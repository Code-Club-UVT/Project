# Step 3: filter the teacher's answers again with stricter rules, and split into train/val/test.
# Reads data/mutants.jsonl and data/raw_responses.jsonl (from step 2), writes
# data/train_v2.jsonl, data/val_v2.jsonl, data/test_v2.jsonl. Needs no GPU and no Ollama.
#
# Differences from the filter in 2_responses.py:
# - The split is fixed BEFORE filtering, over all programs that have mutants, so changing the
#   filter never moves a program between train, val and test.
# - Rejects answers that state the bug or give the fix ("instead of ...", "put the return inside
#   the loop", "without a starting value").
# - Accepts more indirect questions that still point at the bug ("when does the function stop",
#   "on the first pass", "greater"), and predict-vs-actual questions.
# The rules were tuned by reading the validation answers only; the test set was not looked at.
import json
import random
import re
from collections import Counter
from tutor_text import SYSTEM_TUTOR, COMPLAINTS

def fixed_split(mutants, seed=0):
    programs = sorted({m["task_id"] for m in mutants})
    random.Random(seed).shuffle(programs)
    n = len(programs)
    test, val = set(programs[: n // 10]), set(programs[n // 10: n // 5])
    return {p: "test" if p in test else "val" if p in val else "train" for p in programs}

def condition(line):
    c = re.match(r"\s*(?:if|elif|while) (.*):", line)
    return c.group(1) if c else None

# Phrases that state the bug instead of asking about it.
STATES_RETURN_IN_LOOP = [
    r"\b(put|puts|putting|place|placed|placing|appear|appears)\b[^?]*\b(inside|within|in)\b[^?]*\bloop",
    r"\breturn\b[^?]*\b(is|sits)\s+(inside|within|in)\b[^?]*\bloop",
    r"\bonly the first\b",
]
STATES_MISSING_VALUE = [
    r"\bwithout\b[^?]*\b(starting|initial|first)\b[^?]*\bvalue",
    r"\bwithout (it |them )?(first |initially )?(having|being given|being assigned|getting)\b",
    r"\bbefore (it's|it is|it has been|being) (assigned|given|initiali[sz]ed|defined)",
    r"\b(never|not|isn't|wasn't|hasn't been) (been )?(initiali[sz]ed|assigned|defined|given a)",
]
# Indirect phrases that still point at the bug, per category.
POINTS_AT = {
    "return_in_loop": [r"\breturn", r"\bstop", r"\bfirst (pass|iteration|time)", r"\bimmediately after", r"\bhow many times"],
    "flipped_comparison": [r"\bcondition", r"\bcompar", r"\b(greater|less|smaller|larger|bigger)\b", r"\b(increasing|decreasing)\b"],
    "shortened_range": [r"\brange", r"\blast\b", r"\bfinal\b", r"\b(every|each) (element|character|item)", r"\ball (the )?(elements|characters|items)"],
    "wrong_remainder": [r"%", r"\bremainder", r"\beven\b", r"\bodd\b"],
    "deleted_update": [r"\bstop", r"\bnever end", r"\binfinite", r"\bforever"],
}
# "What do you expect ... and does the code produce/return that?" is a valid guiding question
# for any bug: the student predicts, then compares with what the code really does.
PREDICT_VS_ACTUAL = r"\bexpect\w*\b[^?]*\b(actual(ly)?|current(ly)?|does the (current )?(code|function)|does it)\b"

def rejection_reason(m, r):
    r = r.replace("’", "'")
    low = r.lower()
    cat = m["category"]
    orig = m["original_line"].strip()
    if len(r) > 350:
        return "too long"
    if "?" not in r:
        return "no question"
    if "```" in r:
        return "contains code"
    # Gives the solution.
    if "instead of" in low:
        return "gives the solution"
    if cat != "return_in_loop" and orig in r:
        return "gives the solution"
    c_orig = condition(m["original_line"])
    if cat in ("flipped_comparison", "wrong_remainder") and c_orig and c_orig in r:
        return "gives the solution"
    if cat == "shortened_range":
        rng = re.search(r"range\(len\(\w+\)\)", orig)
        if rng and rng.group(0) in r:
            return "gives the solution"
    # States the bug.
    if cat == "return_in_loop" and any(re.search(p, low) for p in STATES_RETURN_IN_LOOP):
        return "states the bug"
    if cat in ("deleted_initialization", "deleted_update") and any(re.search(p, low) for p in STATES_MISSING_VALUE):
        return "states the bug"
    # Points at the bug.
    anchors = [rf"\bline {m['bug_line']}\b"] + POINTS_AT.get(cat, [])
    v = re.match(r"(\w+) ", orig)
    if cat in ("wrong_initial_value", "deleted_initialization", "deleted_update") and v:
        anchors.append(rf"\b{re.escape(v.group(1))}\b")
    if cat == "flipped_comparison":
        c = condition(m["modified_line"])
        if c:
            anchors.append(re.escape(c))
    if any(re.search(a, r, re.IGNORECASE) for a in anchors) or re.search(PREDICT_VS_ACTUAL, low):
        return None
    return "does not point at the bug"

def example(m, r):
    return {
        "messages": [
            {"role": "system", "content": SYSTEM_TUTOR},
            {"role": "user", "content": random.Random(m["id"]).choice(COMPLAINTS) + "\n\n" + m["buggy_code"]},
            {"role": "assistant", "content": r},
        ],
        **{k: m[k] for k in ("id", "task_id", "category", "bug_line", "original_line", "modified_line")},
    }

if __name__ == "__main__":
    with open("data/mutants.jsonl", encoding="utf-8") as f:
        mutants = [json.loads(l) for l in f]
    with open("data/raw_responses.jsonl", encoding="utf-8") as f:
        raw = {x["id"]: x["response"] for x in map(json.loads, f)}
    split_of = fixed_split(mutants)

    splits = {"train": [], "val": [], "test": []}
    reasons = Counter()
    kept_per_cat = Counter()
    for m in mutants:
        r = raw[m["id"]]
        reason = rejection_reason(m, r)
        if reason:
            reasons[reason] += 1
            continue
        kept_per_cat[m["category"]] += 1
        splits[split_of[m["task_id"]]].append(example(m, r))

    for s, rows in splits.items():
        random.Random(1).shuffle(rows)
        with open(f"data/{s}_v2.jsonl", "w", encoding="utf-8") as f:
            for e in rows:
                f.write(json.dumps(e, ensure_ascii=False) + "\n")

    kept = sum(kept_per_cat.values())
    print("Answers:", len(mutants), "| kept:", kept, "| rejected:", sum(reasons.values()))
    for reason, c in reasons.most_common():
        print("   rejected,", reason + ":", c)
    print("Kept per category:", dict(kept_per_cat.most_common()))
    print("Programs per split:", dict(Counter(split_of.values())))
    print("Examples: train", len(splits["train"]), "| val", len(splits["val"]), "| test", len(splits["test"]))
