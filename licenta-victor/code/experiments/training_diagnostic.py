# Experiment: run adapter_v2 on its own training examples (train_v2.jsonl) to check whether
# it learned even what it saw. Writes training_diagnostic.txt.
import json
import re
from collections import defaultdict
from unsloth import FastLanguageModel

with open("mutants.jsonl", encoding="utf-8") as f:
    mutants = [json.loads(line) for line in f]

with open("train_v2.jsonl", encoding="utf-8") as f:
    examples = [json.loads(line) for line in f]

def find(user):
    for m in mutants:
        if m["buggy_code"] in user:
            return m
    return None

def anchors(t):
    # Words that show the answer points at the faulty element ("rest" = Romanian "remainder").
    c = t["category"]
    orig = t["original_line"].strip()
    mod = t["modified_line"].strip()
    var = re.match(r"(\w+) ", orig)
    cond = re.match(r"(?:if|while) (.*):", mod)
    if c in ("wrong_initial_value", "deleted_initialization", "deleted_update"):
        return [r"\b" + var.group(1) + r"\b"] if var else []
    if c == "return_in_loop":
        return ["return"]
    if c == "flipped_comparison":
        return [re.escape(cond.group(1))] if cond else []
    if c == "wrong_remainder":
        return [re.escape(cond.group(1)), "%", "rest"] if cond else ["%"]
    if c == "shortened_range":
        return ["range"]
    return []

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name="adapter_v2", max_seq_length=1024, load_in_4bit=True)
FastLanguageModel.for_inference(model)

rows = []
for e in examples:
    messages = e["messages"][:2]
    t = find(messages[1]["content"])
    assert t is not None, "Could not find the label for an example"
    inputs = tokenizer.apply_chat_template(
        messages, add_generation_prompt=True, return_tensors="pt", return_dict=True
    ).to("cuda")
    output = model.generate(**inputs, max_new_tokens=150, do_sample=False)
    r = tokenizer.decode(output[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True)
    rows.append((t, e["messages"][2]["content"], r))

def has_anchor(t, r):
    return any(re.search(x, r, re.IGNORECASE) for x in anchors(t))

# Romanian opening that 16/51 answers started with ("Run the function on paper with a list").
OPENING = "Rulează funcția pe hârtie cu o listă"
per_category = defaultdict(lambda: [0, 0])
for t, _, r in rows:
    per_category[t["category"]][0] += has_anchor(t, r)
    per_category[t["category"]][1] += 1

with open("training_diagnostic.txt", "w", encoding="utf-8") as f:
    for t, target, r in rows:
        f.write(f"[{t['category']}] {t['modified_line'].strip()}\n")
        f.write(f"TARGET: {target}\n")
        f.write(f"MODEL:  {r}\n\n")

total_a = sum(has_anchor(t, r) for t, _, r in rows)
print("\nMentions the faulty element, on training programs:", total_a, "/", len(rows))
for c, (a, n) in sorted(per_category.items()):
    print(f"  {c:<28}{a}/{n}")
print("Distinct answers:", len(set(r for _, _, r in rows)), "of", len(rows))
print("Answers starting with «" + OPENING + "»:", sum(r.startswith(OPENING) for _, _, r in rows), "of", len(rows))
