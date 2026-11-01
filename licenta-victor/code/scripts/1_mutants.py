# Step 1: take correct programs from MBPP and inject controlled bugs (7 categories).
# A mutant is kept only if the program's own tests fail on it.
import json
import os
import random
import re
import subprocess
import sys
from collections import Counter
from datasets import load_dataset

random.seed(0)
DELETE = "DELETE"
MAX_PER_PROGRAM = 3

def indent(line):
    return len(line) - len(line.lstrip(" "))

def flipped_comparison(lines, i):
    line = lines[i]
    if re.match(r"\s*(if|elif|while) ", line):
        if " > " in line:
            return line.replace(" > ", " < ", 1)
        if " < " in line:
            return line.replace(" < ", " > ", 1)
    return None

def shortened_range(lines, i):
    m = re.search(r"range\(len\((\w+)\)\)", lines[i])
    if m:
        return lines[i].replace(m.group(0), "range(len(" + m.group(1) + ") - 1)", 1)
    return None

def wrong_initial_value(lines, i):
    m = re.fullmatch(r"(\s+\w+ = )(0|1)", lines[i])
    if m:
        return m.group(1) + ("1" if m.group(2) == "0" else "0")
    return None

def deleted_initialization(lines, i):
    if re.fullmatch(r"\s+\w+ = (0|1|\[\]|''|\"\"|\w+\[0\])", lines[i]):
        return DELETE
    return None

def wrong_remainder(lines, i):
    line = lines[i]
    if "%" in line and "== 0" in line:
        return line.replace("== 0", "== 1", 1)
    return None

def return_in_loop(lines, i):
    line = lines[i]
    if not line.strip().startswith("return") or i == 0:
        return None
    k = indent(line)
    if indent(lines[i - 1]) <= k:
        return None
    for j in range(i - 1, -1, -1):
        if indent(lines[j]) <= k:
            if indent(lines[j]) == k and re.match(r"\s*(for|while) ", lines[j]):
                return " " * indent(lines[j + 1]) + line.strip()
            return None
    return None

def deleted_update(lines, i):
    # Delete `x += 1` only if x appears in the condition of a while loop above it,
    # so the loop really stops terminating.
    m = re.fullmatch(r"\s+(\w+) [-+]= 1", lines[i])
    if not m:
        return None
    var = m.group(1)
    for x in lines[:i]:
        c = re.match(r"\s*while (.*):", x)
        if c and re.search(rf"\b{var}\b", c.group(1)):
            return DELETE
    return None

MUTATIONS = [
    ("flipped_comparison", flipped_comparison),
    ("shortened_range", shortened_range),
    ("wrong_initial_value", wrong_initial_value),
    ("deleted_initialization", deleted_initialization),
    ("wrong_remainder", wrong_remainder),
    ("return_in_loop", return_in_loop),
    ("deleted_update", deleted_update),
]

def run(script):
    try:
        r = subprocess.run([sys.executable, "-c", script],
                           capture_output=True, text=True, timeout=2)
        return r.returncode
    except subprocess.TimeoutExpired:
        return "timeout"

def compiles(code):
    try:
        compile(code, "<code>", "exec")
        return True
    except SyntaxError:
        return False

os.makedirs("data", exist_ok=True)
mbpp = load_dataset("google-research-datasets/mbpp", "full")
programs = [ex for split in mbpp for ex in mbpp[split]]

mutants = []
used = 0
for p in programs:
    code = p["code"].replace("\r\n", "\n").replace("\t", "    ")
    lines = [l.rstrip() for l in code.split("\n") if l.strip()]
    if len(lines) > 15 or not any(re.match(r"\s*(for|while) ", l) for l in lines):
        continue
    code = "\n".join(lines)
    tests = "\n".join(p["test_list"])
    setup = p["test_setup_code"] or ""
    if run(setup + "\n" + code + "\n" + tests) != 0:
        continue
    used += 1
    candidates = []
    for i in range(len(lines)):
        for category, f in MUTATIONS:
            new = f(lines, i)
            if new is None:
                continue
            if new == DELETE:
                new_lines = lines[:i] + lines[i + 1:]
                modified = "(line deleted)"
            else:
                new_lines = lines[:i] + [new] + lines[i + 1:]
                modified = new
            candidates.append((category, i, modified, "\n".join(new_lines)))
    random.shuffle(candidates)
    kept = 0
    seen = set()
    for category, i, modified, buggy in candidates:
        if kept == MAX_PER_PROGRAM:
            break
        if buggy in seen or not compiles(buggy):
            continue
        seen.add(buggy)
        if run(setup + "\n" + buggy + "\n" + tests) == 0:
            continue
        mutants.append({
            "id": len(mutants) + 1,
            "task_id": p["task_id"],
            "category": category,
            "bug_line": i + 1,
            "original_line": lines[i],
            "modified_line": modified,
            "buggy_code": buggy,
            "correct_code": code,
        })
        kept += 1

with open("data/mutants.jsonl", "w", encoding="utf-8") as f:
    for m in mutants:
        f.write(json.dumps(m, ensure_ascii=False) + "\n")

print("MBPP programs used:", used)
print("Buggy programs:", len(mutants))
for c, n in Counter(m["category"] for m in mutants).most_common():
    print("  ", c, n)
