# Step 2: the teacher model (gemma3:12b via Ollama) writes one Socratic tutor question
# per mutant, in English. A rule-based filter drops bad answers, then the kept examples
# are split into train/val/test by MBPP task_id, so no program appears in two splits.
import json
import os
import random
import re
import sys
from collections import Counter
import requests

MODEL = "gemma3:12b"
URL = "http://localhost:11434/api/chat"
RAW = "data/raw_responses.jsonl"
random.seed(0)

SYSTEM_TUTOR = ("You are a programming tutor. Never give the solution. "
                "Ask questions that guide the student.")
COMPLAINTS = [
    "My function doesn't work and I don't understand why.",
    "Something is wrong in my code, can you help me?",
    "The result is not what I expected.",
    "I don't know what I did wrong here.",
]
DESCRIPTION = {
    "flipped_comparison": "a comparison was flipped",
    "shortened_range": "the loop stops one step too early",
    "wrong_initial_value": "a variable gets a wrong starting value",
    "deleted_initialization": "a variable is used without ever getting a starting value",
    "wrong_remainder": "the remainder test checks exactly the opposite",
    "return_in_loop": "the return statement is inside the loop, so the function stops too early",
    "deleted_update": "the variable in the while condition never changes, so the loop never ends",
}
# One question style per mutant, each with a required opening, so the questions do not
# all follow the same template.
STYLES = [
    "Ask what result they expect for a concrete input example that you choose. Start with \"If\".",
    "Ask them to trace step by step how the value of one variable changes. Start with \"How\".",
    "Ask what happens on the first or the last pass through the loop. Start with \"On the first\" or \"On the last\".",
    "Ask what role a specific line or variable plays in the program. Start with \"What do you think\".",
    "Ask what they would see if they printed a specific value. Start with \"If you added a print\".",
    "Ask why they chose a specific value or line. Start with \"Why\".",
]
FORBIDDEN_STARTS = ("notice", "look at", "take a look")

def teacher_request(m):
    style = random.Random(m["id"]).choice(STYLES)
    if m["modified_line"] == "(line deleted)":
        what = f"Line `{m['original_line'].strip()}` was deleted (it was line {m['bug_line']})."
    else:
        what = f"On line {m['bug_line']}, `{m['original_line'].strip()}` was replaced with `{m['modified_line'].strip()}`."
    return (
        "A beginner student wrote the code below and it contains a mistake.\n\n"
        f"```python\n{m['buggy_code']}\n```\n\n"
        f"What is wrong (only for you, the student does not know): {DESCRIPTION[m['category']]}. {what}\n\n"
        "Write a single short question, at most two sentences, that you would ask the student "
        "so they discover the mistake on their own. Do not say what the mistake is, do not give the solution, "
        "do not write code and do not quote the correct version. You may name the variable or the line to look at. "
        f"{style} Do not start with \"Notice\", \"Look at\" or \"Take a look\". "
        "Write in clear, simple English for a beginner. Reply with the question only."
    )

def ask(text):
    r = requests.post(URL, timeout=300, json={
        "model": MODEL, "stream": False,
        "messages": [{"role": "user", "content": text}],
        "options": {"temperature": 0.7, "num_predict": 150},
    })
    r.raise_for_status()
    return r.json()["message"]["content"].strip().strip('"“”').strip()

def ask_varied(text):
    # Retry up to 3 times if the answer starts with a forbidden opening.
    for _ in range(3):
        r = ask(text)
        if not r.lower().startswith(FORBIDDEN_STARTS):
            return r
    return r

def condition(line):
    c = re.match(r"\s*(?:if|elif|while) (.*):", line)
    return c.group(1) if c else None

def rejection_reason(m, r):
    if len(r) > 350:
        return "too long"
    if "?" not in r:
        return "no question"
    if "```" in r:
        return "contains code"
    orig = m["original_line"].strip()
    cat = m["category"]
    if cat != "return_in_loop" and orig in r:
        return "gives the solution"
    c_orig = condition(m["original_line"])
    if cat in ("flipped_comparison", "wrong_remainder") and c_orig and c_orig in r:
        return "gives the solution"
    # The answer must point at the bug: the line number or a keyword of the category.
    anchors = [rf"line {m['bug_line']}\b"]
    v = re.match(r"(\w+) ", orig)
    if cat in ("wrong_initial_value", "deleted_initialization", "deleted_update") and v:
        anchors.append(rf"\b{re.escape(v.group(1))}\b")
    elif cat == "return_in_loop":
        anchors.append("return")
    elif cat == "shortened_range":
        anchors += ["range", r"\blast\b"]
    elif cat == "flipped_comparison":
        c = condition(m["modified_line"])
        if c:
            anchors.append(re.escape(c))
        anchors += ["condition", "compar"]
    elif cat == "wrong_remainder":
        anchors += ["%", "remainder", r"\beven\b", r"\bodd\b"]
    if not any(re.search(a, r, re.IGNORECASE) for a in anchors):
        return "does not point at the bug"
    return None

try:
    requests.get("http://localhost:11434", timeout=5)
except requests.exceptions.ConnectionError:
    sys.exit("Ollama is not running. Open another Ubuntu window, run `ollama serve` and try again.")

with open("data/mutants.jsonl", encoding="utf-8") as f:
    mutants = [json.loads(l) for l in f]

done = {}
if os.path.exists(RAW):
    with open(RAW, encoding="utf-8") as f:
        for l in f:
            x = json.loads(l)
            done[x["id"]] = x["response"]

with open(RAW, "a", encoding="utf-8") as f:
    for n, m in enumerate(mutants, 1):
        if m["id"] in done:
            continue
        r = ask_varied(teacher_request(m))
        done[m["id"]] = r
        f.write(json.dumps({"id": m["id"], "response": r}, ensure_ascii=False) + "\n")
        f.flush()
        if n % 25 == 0:
            print(f"{n}/{len(mutants)} done", flush=True)

kept, reasons = [], Counter()
for m in mutants:
    r = done[m["id"]]
    reason = rejection_reason(m, r)
    if reason:
        reasons[reason] += 1
        continue
    kept.append((m, r))

programs = sorted({m["task_id"] for m, _ in kept})
random.shuffle(programs)
n = len(programs)
test_ids = set(programs[: n // 10])
val_ids = set(programs[n // 10: n // 5])

splits = {"train": [], "val": [], "test": []}
for m, r in kept:
    s = "test" if m["task_id"] in test_ids else "val" if m["task_id"] in val_ids else "train"
    splits[s].append({
        "messages": [
            {"role": "system", "content": SYSTEM_TUTOR},
            {"role": "user", "content": random.choice(COMPLAINTS) + "\n\n" + m["buggy_code"]},
            {"role": "assistant", "content": r},
        ],
        **{k: m[k] for k in ("id", "task_id", "category", "bug_line", "original_line", "modified_line")},
    })

for s, rows in splits.items():
    random.shuffle(rows)
    with open(f"data/{s}.jsonl", "w", encoding="utf-8") as f:
        for e in rows:
            f.write(json.dumps(e, ensure_ascii=False) + "\n")

print("\nGenerated:", len(mutants), "| kept:", len(kept), "| rejected:", sum(reasons.values()))
for reason, c in reasons.most_common():
    print("   rejected,", reason + ":", c)
print("Kept per category:", dict(Counter(m["category"] for m, _ in kept)))
print("Train:", len(splits["train"]), "| val:", len(splits["val"]), "| test:", len(splits["test"]))
print("\n--- 6 random examples ---")
for e in random.sample(splits["train"], min(6, len(splits["train"]))):
    print(f"\n[{e['category']}] line {e['bug_line']}: {e['modified_line'].strip()}")
    print("TUTOR:", e["messages"][2]["content"])
