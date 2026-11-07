# Bug-injection rules and helpers shared by 1_mutants.py and 1b_extra_mutants.py.
# Each mutation takes the program's lines and an index i, and returns the new line,
# DELETE (remove line i), or None (does not apply).
import re
import subprocess
import sys

DELETE = "DELETE"

def indent(line):
    return len(line) - len(line.lstrip(" "))

def condition(line):
    c = re.match(r"\s*(?:if|elif|while) (.*):", line)
    return c.group(1) if c else None

# ---- The 7 original mutations (used by 1_mutants.py; do not change, or data/mutants.jsonl changes) ----

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

def in_while_condition(lines, i, var):
    # True if `var` appears in the condition of a while loop above line i.
    for x in lines[:i]:
        c = re.match(r"\s*while (.*):", x)
        if c and re.search(rf"\b{re.escape(var)}\b", c.group(1)):
            return True
    return False

def deleted_update(lines, i):
    # Delete `x += 1` only if x appears in the condition of a while loop above it,
    # so the loop really stops terminating.
    m = re.fullmatch(r"\s+(\w+) [-+]= 1", lines[i])
    if m and in_while_condition(lines, i, m.group(1)):
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

# ---- Wider versions of the rare categories (used by 1b_extra_mutants.py) ----
# Same meaning as the original category, more code patterns.

def flipped_comparison_eq(lines, i):
    line = lines[i]
    if re.match(r"\s*(if|elif|while) ", line):
        if " >= " in line:
            return line.replace(" >= ", " <= ", 1)
        if " <= " in line:
            return line.replace(" <= ", " >= ", 1)
    return None

def shortened_range_wide(lines, i):
    # The loop stops one step too early: range(n) -> range(n - 1), range(a, len(x)) -> range(a, len(x) - 1),
    # range(a, n) -> range(a, n - 1), range(n + 1) -> range(n). Only inside a for loop header.
    line = lines[i]
    if not re.match(r"\s*for .* in range\(", line):
        return None
    m = re.search(r"range\(([^()]*?(?:\([^()]*\))?[^()]*?)\)", line)
    if not m:
        return None
    args = m.group(1)
    parts = [p.strip() for p in args.split(",")]
    if len(parts) > 2:  # has a step argument
        return None
    end = parts[-1]
    if re.fullmatch(r"len\(\w+\)", end) and len(parts) == 1:
        return None  # already covered by shortened_range
    if re.fullmatch(r"(\w+) \+ 1", end):
        new_end = re.fullmatch(r"(\w+) \+ 1", end).group(1)
    elif re.fullmatch(r"len\(\w+\)|[A-Za-z_]\w*", end):
        new_end = end + " - 1"
    else:
        return None
    new_args = ", ".join(parts[:-1] + [new_end])
    return line.replace(m.group(0), "range(" + new_args + ")", 1)

def wrong_remainder_ne(lines, i):
    line = lines[i]
    if re.search(r"% 2 != 0", line):
        return line.replace("% 2 != 0", "% 2 == 0", 1)
    if re.search(r"% 2 == 1", line):
        return line.replace("% 2 == 1", "% 2 == 0", 1)
    return None

def deleted_update_wide(lines, i):
    # Delete any update of a variable that appears in a while condition above it:
    # x += k, x -= k, x //= k, x >>= k, x /= k, x = x - k, x = x // k, ...
    m = (re.fullmatch(r"\s+(\w+) (\+|-|//|/|>>|<<|\*)= \S+", lines[i])
         or re.fullmatch(r"\s+(\w+) = \(?\1 (\+|-|//|/|>>|<<|\*) [^=]+\)?", lines[i]))
    if not m or re.fullmatch(r"\s+\w+ [-+]= 1", lines[i]):
        return None  # x += 1 / x -= 1 already covered by deleted_update
    if in_while_condition(lines, i, m.group(1)):
        return DELETE
    return None

RARE_MUTATIONS = [
    ("flipped_comparison", flipped_comparison),
    ("flipped_comparison", flipped_comparison_eq),
    ("shortened_range", shortened_range),
    ("shortened_range", shortened_range_wide),
    ("wrong_remainder", wrong_remainder),
    ("wrong_remainder", wrong_remainder_ne),
    ("deleted_update", deleted_update),
    ("deleted_update", deleted_update_wide),
]

# ---- Helpers ----

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

def load_programs():
    # MBPP programs with at most 15 lines, at least one for/while loop, and passing tests.
    # Returns (task_id, lines, code, setup, tests) in MBPP order.
    from datasets import load_dataset
    mbpp = load_dataset("google-research-datasets/mbpp", "full")
    out = []
    for p in (ex for split in mbpp for ex in mbpp[split]):
        code = p["code"].replace("\r\n", "\n").replace("\t", "    ")
        lines = [l.rstrip() for l in code.split("\n") if l.strip()]
        if len(lines) > 15 or not any(re.match(r"\s*(for|while) ", l) for l in lines):
            continue
        code = "\n".join(lines)
        tests = "\n".join(p["test_list"])
        setup = p["test_setup_code"] or ""
        if run(setup + "\n" + code + "\n" + tests) != 0:
            continue
        out.append((p["task_id"], lines, code, setup, tests))
    return out

def candidates(lines, mutations):
    # All (category, line index, modified line, buggy code) the mutations produce.
    out = []
    for i in range(len(lines)):
        for category, f in mutations:
            new = f(lines, i)
            if new is None:
                continue
            if new == DELETE:
                new_lines = lines[:i] + lines[i + 1:]
                modified = "(line deleted)"
            else:
                new_lines = lines[:i] + [new] + lines[i + 1:]
                modified = new
            out.append((category, i, modified, "\n".join(new_lines)))
    return out
