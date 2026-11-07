# Step 1: take correct programs from MBPP and inject controlled bugs (7 categories).
# A mutant is kept only if the program's own tests fail on it. Max 3 mutants per program.
# The mutation rules are in mutations.py.
import json
import os
import random
from collections import Counter
from mutations import MUTATIONS, candidates, compiles, load_programs, run

random.seed(0)
MAX_PER_PROGRAM = 3

os.makedirs("data", exist_ok=True)
programs = load_programs()

mutants = []
for task_id, lines, code, setup, tests in programs:
    cands = candidates(lines, MUTATIONS)
    random.shuffle(cands)
    kept = 0
    seen = set()
    for category, i, modified, buggy in cands:
        if kept == MAX_PER_PROGRAM:
            break
        if buggy in seen or not compiles(buggy):
            continue
        seen.add(buggy)
        if run(setup + "\n" + buggy + "\n" + tests) == 0:
            continue
        mutants.append({
            "id": len(mutants) + 1,
            "task_id": task_id,
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

print("MBPP programs used:", len(programs))
print("Buggy programs:", len(mutants))
for c, n in Counter(m["category"] for m in mutants).most_common():
    print("  ", c, n)
