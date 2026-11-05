# Step 1b: extra mutants for the rare categories (flipped_comparison, shortened_range,
# wrong_remainder, deleted_update), using the original rules plus wider patterns
# (see RARE_MUTATIONS in mutations.py). Writes data/mutants_extra.jsonl.
# - Does not change data/mutants.jsonl; new ids continue after the last existing id.
# - Skips any buggy code that is already in data/mutants.jsonl.
# - A mutant is kept only if the program's own tests fail on it. Max 3 extra per program.
import json
import random
from collections import Counter
from mutations import RARE_MUTATIONS, candidates, compiles, load_programs, run

MAX_EXTRA_PER_PROGRAM = 3
rng = random.Random(1)

with open("data/mutants.jsonl", encoding="utf-8") as f:
    existing = [json.loads(l) for l in f]
existing_code = {m["buggy_code"] for m in existing}
next_id = max(m["id"] for m in existing) + 1

programs = load_programs()
extra = []
available = Counter()
for task_id, lines, code, setup, tests in programs:
    cands = candidates(lines, RARE_MUTATIONS)
    rng.shuffle(cands)
    seen = set(existing_code)
    good = []
    for category, i, modified, buggy in cands:
        if buggy in seen or not compiles(buggy):
            continue
        seen.add(buggy)
        if run(setup + "\n" + buggy + "\n" + tests) == 0:
            continue
        good.append((category, i, modified, buggy))
    available.update(c for c, *_ in good)
    # Prefer the rarest categories first, so the few deleted_update / shortened_range are not lost.
    order = {"deleted_update": 0, "shortened_range": 1, "wrong_remainder": 2, "flipped_comparison": 3}
    good.sort(key=lambda g: order[g[0]])
    for category, i, modified, buggy in good[:MAX_EXTRA_PER_PROGRAM]:
        extra.append({
            "id": next_id,
            "task_id": task_id,
            "category": category,
            "bug_line": i + 1,
            "original_line": lines[i],
            "modified_line": modified,
            "buggy_code": buggy,
            "correct_code": code,
        })
        next_id += 1

with open("data/mutants_extra.jsonl", "w", encoding="utf-8") as f:
    for m in extra:
        f.write(json.dumps(m, ensure_ascii=False) + "\n")

old_programs = {m["task_id"] for m in existing}
print("MBPP programs checked:", len(programs))
print("New failing rare-category mutants available:", dict(available.most_common()))
print("Extra mutants kept:", len(extra), "| from programs:", len({m["task_id"] for m in extra}),
      "| of which programs with no earlier mutant:", len({m["task_id"] for m in extra} - old_programs))
for c, n in Counter(m["category"] for m in extra).most_common():
    print("  ", c, n)
