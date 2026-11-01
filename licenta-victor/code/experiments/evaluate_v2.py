# Experiment: evaluate base / toy / v2 adapters on test_v2.jsonl (9 mutants).
# Writes comparison_v2.txt and prints a table:
# A = mentions the faulty element | C = code block | S = contains the correct line.
# The Romanian system prompt is kept verbatim: it is the input the recorded results came from.
import json
import re
import torch
from unsloth import FastLanguageModel

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

with open("test_v2.jsonl", encoding="utf-8") as f:
    tests = [json.loads(line) for line in f]

models = [
    ("base", "unsloth/Qwen2.5-1.5B-Instruct"),
    ("toy", "adapter_toy"),
    ("v2", "adapter_v2"),
]

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

def run(path):
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=path, max_seq_length=1024, load_in_4bit=True)
    FastLanguageModel.for_inference(model)
    answers = []
    for t in tests:
        messages = [
            {"role": "system", "content": SYSTEM},
            {"role": "user", "content": t["complaint"] + "\n\n" + t["buggy_code"]},
        ]
        inputs = tokenizer.apply_chat_template(
            messages, add_generation_prompt=True, return_tensors="pt", return_dict=True
        ).to("cuda")
        output = model.generate(**inputs, max_new_tokens=150, do_sample=False)
        answers.append(tokenizer.decode(
            output[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True))
    del model
    torch.cuda.empty_cache()
    return answers

results = {name: run(path) for name, path in models}

def check(t, r):
    anchor = any(re.search(a, r, re.IGNORECASE) for a in anchors(t))
    code = "```" in r
    leaked = t["original_line"].strip() in r
    return anchor, code, leaked

with open("comparison_v2.txt", "w", encoding="utf-8") as f:
    for i, t in enumerate(tests):
        f.write(f"\n===== TEST {i+1} ({t['category']}, line {t['bug_line']}): {t['modified_line'].strip()}\n")
        f.write(t["buggy_code"] + "\n")
        for name, _ in models:
            f.write(f"\n--- {name.upper()} ---\n{results[name][i]}\n")

print("\nA = mentions the faulty element | C = code block | S = contains the correct line")
print(f"{'#':<4}{'category':<28}" + "".join(f"{name:<10}" for name, _ in models))
for i, t in enumerate(tests):
    cells = ""
    for name, _ in models:
        a, c, s = check(t, results[name][i])
        cells += f"{'A' if a else '-'}{'C' if c else '-'}{'S' if s else '-'}".ljust(10)
    print(f"{i+1:<4}{t['category']:<28}" + cells)

print()
for name, _ in models:
    v = [check(t, r) for t, r in zip(tests, results[name])]
    print(f"{name:<9} mentions element: {sum(x[0] for x in v)}/9 | code block: {sum(x[1] for x in v)}/9 | correct line: {sum(x[2] for x in v)}/9")
