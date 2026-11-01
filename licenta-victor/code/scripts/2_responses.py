# Step 2: the teacher model (gemma3:12b via Ollama) writes one Socratic tutor question
# per mutant, in English. Generation only: filtering and the train/val/test split are done
# by 3_filter.py. Resumable: answers already in the output file are skipped.
#
#   python scripts/2_responses.py                         # data/mutants.jsonl -> data/raw_responses.jsonl
#   python scripts/2_responses.py --mutants data/mutants_extra.jsonl --raw data/raw_responses_extra.jsonl
import argparse
import json
import os
import random
import sys
import requests

MODEL = "gemma3:12b"
URL = "http://localhost:11434/api/chat"

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

parser = argparse.ArgumentParser()
parser.add_argument("--mutants", default="data/mutants.jsonl")
parser.add_argument("--raw", default="data/raw_responses.jsonl")
args = parser.parse_args()

try:
    requests.get("http://localhost:11434", timeout=5)
except requests.exceptions.ConnectionError:
    sys.exit("Ollama is not running. Open another Ubuntu window, run `ollama serve` and try again.")

with open(args.mutants, encoding="utf-8") as f:
    mutants = [json.loads(l) for l in f]

done = {}
if os.path.exists(args.raw):
    with open(args.raw, encoding="utf-8") as f:
        for l in f:
            x = json.loads(l)
            done[x["id"]] = x["response"]
print(f"{len(mutants)} mutants in {args.mutants}, {sum(m['id'] in done for m in mutants)} already answered in {args.raw}", flush=True)

with open(args.raw, "a", encoding="utf-8") as f:
    for n, m in enumerate(mutants, 1):
        if m["id"] in done:
            continue
        r = ask_varied(teacher_request(m))
        done[m["id"]] = r
        f.write(json.dumps({"id": m["id"], "response": r}, ensure_ascii=False) + "\n")
        f.flush()
        if n % 25 == 0:
            print(f"{n}/{len(mutants)} done", flush=True)

print(f"\nDone: {sum(m['id'] in done for m in mutants)}/{len(mutants)} answered in {args.raw}")
print("Next: python scripts/3_filter.py")
print("\n--- 6 random answers ---")
for m in random.Random(0).sample(mutants, min(6, len(mutants))):
    print(f"\n[{m['category']}] line {m['bug_line']}: {m['modified_line'].strip()}")
    print("TUTOR:", done[m["id"]])
