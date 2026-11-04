# Step 5: let each model write a tutor answer for every example in data/val_v3.jsonl and judge the
# answers with the same rules as the data filter (3_filter.py: rejection_reason).
# Compares the base model (no training) with the adapter saved after each epoch.
# Only the validation set is used; the test set is not read.
# Writes every answer to results/<date>_5_eval_val_answers.jsonl and prints a summary table.
import argparse
import importlib
import json
import os
import subprocess
import sys
import time
from collections import Counter

sys.path.insert(0, "scripts")
rejection_reason = importlib.import_module("3_filter").rejection_reason

parser = argparse.ArgumentParser()
parser.add_argument("--run", default="outputs/v3_r16_lr2e-4")
parser.add_argument("--base", default="unsloth/Qwen2.5-Coder-7B-Instruct-bnb-4bit")
parser.add_argument("--checkpoints", default="72,144,216")
parser.add_argument("--val", default="data/val_v3.jsonl")
parser.add_argument("--limit", type=int, default=0, help="only for a quick test run")
parser.add_argument("--out", default=time.strftime("results/%Y-%m-%d_%H%M_5_eval_val_answers.jsonl"))
parser.add_argument("--one", nargs=2, metavar=("PATH", "ANSWERS_FILE"), help=argparse.SUPPRESS)
args = parser.parse_args()

val = [json.loads(line) for line in open(args.val, encoding="utf-8")]
if args.limit:
    val = val[: args.limit]
models = [("base", args.base)] + [
    (f"step{c}", f"{args.run}/checkpoint-{c}") for c in args.checkpoints.split(",")
]

def generate(path):
    from unsloth import FastLanguageModel
    model, tokenizer = FastLanguageModel.from_pretrained(model_name=path, max_seq_length=512, load_in_4bit=True)
    FastLanguageModel.for_inference(model)
    tokenizer.padding_side = "left"
    answers = []
    for i in range(0, len(val), 8):
        batch = val[i: i + 8]
        prompts = [tokenizer.apply_chat_template(ex["messages"][:2], tokenize=False, add_generation_prompt=True)
                   for ex in batch]
        inputs = tokenizer(prompts, return_tensors="pt", padding=True).to("cuda")
        # Greedy decoding: the same input always gives the same answer.
        out = model.generate(**inputs, max_new_tokens=200, do_sample=False)
        answers += tokenizer.batch_decode(out[:, inputs["input_ids"].shape[1]:], skip_special_tokens=True)
    return [a.strip() for a in answers]

if args.one:
    # Child process: one model only. Its GPU memory is freed when the process exits.
    json.dump(generate(args.one[0]), open(args.one[1], "w", encoding="utf-8"))
    sys.exit(0)

rows, summary = [], {}
tmp = args.out + ".part"
for name, path in models:
    start = time.time()
    cmd = [sys.executable, __file__, "--val", args.val, "--limit", str(args.limit), "--one", path, tmp]
    subprocess.run(cmd, check=True)
    answers = json.load(open(tmp, encoding="utf-8"))
    reasons = Counter()
    per_cat = Counter()
    for ex, a in zip(val, answers):
        reason = rejection_reason(ex, a) or "ok"
        reasons[reason] += 1
        if reason == "ok":
            per_cat[ex["category"]] += 1
        rows.append({"model": name, "id": ex["id"], "category": ex["category"], "verdict": reason,
                     "answer": a, "teacher": ex["messages"][2]["content"]})
    summary[name] = (reasons, per_cat, sum(len(a) for a in answers) / len(answers), time.time() - start)
    print(f"{name}: done in {(time.time() - start) / 60:.1f} min", flush=True)

os.remove(tmp)
with open(args.out, "w", encoding="utf-8") as f:
    for r in rows:
        f.write(json.dumps(r, ensure_ascii=False) + "\n")

n = len(val)
cats = Counter(ex["category"] for ex in val)
print(f"\n=== Summary on {n} val examples (verdict = same rules as the data filter) ===")
verdicts = ["ok", "does not point at the bug", "states the bug", "gives the solution",
            "no question", "contains code", "too long"]
print(f"{'verdict':<28}" + "".join(f"{m:>9}" for m, _ in models))
for v in verdicts:
    print(f"{v:<28}" + "".join(f"{summary[m][0][v]:>9}" for m, _ in models))
print(f"{'mean length (chars)':<28}" + "".join(f"{summary[m][2]:>9.0f}" for m, _ in models))
print("\nok per category:")
for c, k in cats.most_common():
    print(f"  {c + f' ({k})':<26}" + "".join(f"{summary[m][1][c]:>9}" for m, _ in models))
print("\nAnswers saved to", args.out)
