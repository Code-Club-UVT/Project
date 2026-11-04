# Step 4: QLoRA training of the tutor on data/train_v3.jsonl, validation loss on data/val_v3.jsonl.
# The test set is not read here.
# Saves one adapter checkpoint per epoch in outputs/<name>/ (so the best epoch can be chosen on val)
# and the loss history in outputs/<name>/loss_history.json.
import argparse
import json
import os
import time

from unsloth import FastLanguageModel, is_bfloat16_supported
from unsloth.chat_templates import train_on_responses_only
from datasets import load_dataset
from trl import SFTTrainer, SFTConfig
import torch

parser = argparse.ArgumentParser()
parser.add_argument("--name", default="v3_r16_lr2e-4")
parser.add_argument("--model", default="unsloth/Qwen2.5-Coder-7B-Instruct-bnb-4bit")
parser.add_argument("--train", default="data/train_v3.jsonl")
parser.add_argument("--val", default="data/val_v3.jsonl")
parser.add_argument("--epochs", type=int, default=3)
parser.add_argument("--lr", type=float, default=2e-4)
parser.add_argument("--r", type=int, default=16)
parser.add_argument("--seed", type=int, default=3407)
parser.add_argument("--max-steps", type=int, default=-1, help="only for a quick test run")
args = parser.parse_args()

out_dir = os.path.join("outputs", args.name)
if os.path.exists(out_dir):
    raise SystemExit(f"{out_dir} already exists: choose another --name (nothing is overwritten).")

print("Settings:", vars(args))
model, tokenizer = FastLanguageModel.from_pretrained(
    model_name=args.model,
    max_seq_length=512,  # longest example is 304 tokens
    load_in_4bit=True,
)
model = FastLanguageModel.get_peft_model(
    model, r=args.r, lora_alpha=args.r,
    target_modules=["q_proj", "k_proj", "v_proj", "o_proj",
                    "gate_proj", "up_proj", "down_proj"],
    lora_dropout=0, bias="none",
    use_gradient_checkpointing="unsloth",
    random_state=args.seed,
)

data = load_dataset("json", data_files={"train": args.train, "validation": args.val})
data = data.map(
    lambda ex: {"text": tokenizer.apply_chat_template(ex["messages"], tokenize=False)},
    remove_columns=data["train"].column_names,
)
print("Examples: train", len(data["train"]), "| val", len(data["validation"]))

trainer = SFTTrainer(
    model=model,
    processing_class=tokenizer,
    train_dataset=data["train"],
    eval_dataset=data["validation"],
    args=SFTConfig(
        dataset_text_field="text",
        max_length=512,
        per_device_train_batch_size=4,
        gradient_accumulation_steps=2,  # 8 examples per update
        per_device_eval_batch_size=8,
        num_train_epochs=args.epochs,
        max_steps=args.max_steps,
        learning_rate=args.lr,
        warmup_steps=5,
        lr_scheduler_type="linear",
        optim="adamw_8bit",
        fp16=not is_bfloat16_supported(),
        bf16=is_bfloat16_supported(),
        logging_steps=4,
        eval_strategy="steps",
        eval_steps=12,
        save_strategy="epoch",
        report_to="none",
        output_dir=out_dir,
        seed=args.seed,
    ),
)

# Loss only on the tutor's answer, not on the system prompt or the student's code.
trainer = train_on_responses_only(
    trainer,
    instruction_part="<|im_start|>user\n",
    response_part="<|im_start|>assistant\n",
)

start = time.time()
trainer.train()
minutes = (time.time() - start) / 60

# Final validation loss of the last epoch (the per-epoch checkpoints are in out_dir/checkpoint-*).
history = list(trainer.state.log_history)
final = trainer.evaluate()
with open(os.path.join(out_dir, "loss_history.json"), "w") as f:
    json.dump(history, f, indent=1)

print("\n=== Summary ===")
print(f"Training time: {minutes:.1f} min | peak VRAM: {torch.cuda.max_memory_allocated() / 1e9:.2f} GB")
print("Validation loss by step (epoch):")
for h in history:
    if "eval_loss" in h:
        print(f"  step {h['step']:>4} (epoch {h['epoch']:.2f}): {h['eval_loss']:.4f}")
print("Final val loss:", round(final["eval_loss"], 4))
print("Checkpoints:", sorted(d for d in os.listdir(out_dir) if d.startswith("checkpoint")))
