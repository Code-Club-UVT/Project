# Experiment: mutation-based bug generator on 10 small hand-written programs
# (7 mutation categories, each mutant checked by running it). Writes mutants.jsonl.
# The student programs keep their Romanian names: they are the data.
import json
import re
import subprocess
import sys
from collections import Counter

DELETE = "DELETE"

programs = [
    {"name": "suma", "call": "print(suma([3, 1, 4, 1, 5]))", "code": """def suma(lista):
    total = 0
    for x in lista:
        total += x
    return total"""},
    {"name": "maxim", "call": "print(maxim([3, 9, 4, 1, 5]))", "code": """def maxim(lista):
    m = lista[0]
    for x in lista:
        if x > m:
            m = x
    return m"""},
    {"name": "numara_pare", "call": "print(numara_pare([2, 4, 6, 7, 9]))", "code": """def numara_pare(lista):
    contor = 0
    for x in lista:
        if x % 2 == 0:
            contor += 1
    return contor"""},
    {"name": "produs", "call": "print(produs([2, 3, 4]))", "code": """def produs(lista):
    rezultat = 1
    for x in lista:
        rezultat *= x
    return rezultat"""},
    {"name": "afiseaza", "call": "afiseaza([10, 20, 30])", "code": """def afiseaza(lista):
    for i in range(len(lista)):
        print(lista[i])"""},
    {"name": "numara_invers", "call": "numara_invers(3)", "code": """def numara_invers(n):
    while n > 0:
        print(n)
        n -= 1
    print("Gata")"""},
    {"name": "contor_pozitive", "call": "print(contor_pozitive([3, -1, 4, -5, 2]))", "code": """def contor_pozitive(lista):
    contor = 0
    for x in lista:
        if x > 0:
            contor += 1
    return contor"""},
    {"name": "cauta", "call": "print(cauta([5, 8, 2, 9], 9))", "code": """def cauta(lista, x):
    for i in range(len(lista)):
        if lista[i] == x:
            return i
    return -1"""},
    {"name": "media", "call": "print(media([2, 4, 6]))", "code": """def media(lista):
    total = 0
    for x in lista:
        total += x
    return total / len(lista)"""},
    {"name": "suma_pare", "call": "print(suma_pare([2, 3, 4, 5, 6]))", "code": """def suma_pare(lista):
    total = 0
    for x in lista:
        if x % 2 == 0:
            total += x
    return total"""},
]

def flipped_comparison(lines, i):
    line = lines[i]
    if re.match(r"\s*(if|while) ", line):
        if " > " in line:
            return line.replace(" > ", " < ", 1)
        if " < " in line:
            return line.replace(" < ", " > ", 1)
    return None

def shortened_range(lines, i):
    m = re.search(r"range\(len\((\w+)\)\)", lines[i])
    if m:
        return lines[i].replace(m.group(0), "range(len(" + m.group(1) + ") - 1)")
    return None

def wrong_initial_value(lines, i):
    m = re.fullmatch(r"(\s+\w+ = )(0|1)", lines[i])
    if m:
        return m.group(1) + ("1" if m.group(2) == "0" else "0")
    return None

def deleted_initialization(lines, i):
    if re.fullmatch(r"\s+\w+ = (0|1|\w+\[0\])", lines[i]):
        return DELETE
    return None

def wrong_remainder(lines, i):
    line = lines[i]
    if "%" in line and "== 0" in line:
        return line.replace("== 0", "== 1")
    return None

def return_in_loop(lines, i):
    line = lines[i]
    if line.startswith("    return ") and i > 0 and lines[i - 1].startswith("        "):
        return "    " + line
    return None

def deleted_update(lines, i):
    if re.fullmatch(r"\s+\w+ [-+]= 1", lines[i]) and any(
            x.strip().startswith("while ") for x in lines[:i]):
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

def run(code):
    try:
        r = subprocess.run([sys.executable, "-c", code],
                           capture_output=True, text=True, timeout=1)
        return (r.returncode, r.stdout, r.stderr)
    except subprocess.TimeoutExpired:
        return ("timeout", "", "")

mutants = []
rejected = 0

for p in programs:
    lines = p["code"].split("\n")
    reference = run(p["code"] + "\n" + p["call"])
    assert reference[0] == 0, "The correct program does not run: " + p["name"]
    seen = set()
    for i in range(len(lines)):
        for category, function in MUTATIONS:
            new = function(lines, i)
            if new is None:
                continue
            if new == DELETE:
                new_lines = lines[:i] + lines[i + 1:]
                modified = "(line deleted)"
            else:
                new_lines = lines[:i] + [new] + lines[i + 1:]
                modified = new
            buggy = "\n".join(new_lines)
            if buggy in seen:
                continue
            seen.add(buggy)
            result = run(buggy + "\n" + p["call"])
            syntax = "SyntaxError" in result[2] or "IndentationError" in result[2]
            if result[:2] == reference[:2] or syntax:
                rejected += 1
                continue
            mutants.append({
                "id": len(mutants) + 1,
                "program": p["name"],
                "category": category,
                "bug_line": i + 1,
                "original_line": lines[i],
                "modified_line": modified,
                "buggy_code": buggy,
                "correct_code": p["code"],
            })

with open("mutants.jsonl", "w", encoding="utf-8") as f:
    for m in mutants:
        f.write(json.dumps(m, ensure_ascii=False) + "\n")

print("Correct programs:", len(programs))
print("Buggy programs kept:", len(mutants))
print("Rejected (did not change behaviour or were invalid):", rejected)
for category, n in Counter(m["category"] for m in mutants).items():
    print("  ", category, n)

for m in (mutants[0], mutants[len(mutants) // 2]):
    print("\n--- Example ---")
    print("Category:", m["category"], "| bug on line", m["bug_line"])
    print("Original line:", m["original_line"].strip())
    print("Modified line:", m["modified_line"].strip())
    print(m["buggy_code"])
