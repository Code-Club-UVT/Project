from unsloth import FastLanguageModel, is_bfloat16_supported
from unsloth.chat_templates import train_on_responses_only
from datasets import load_dataset
from trl import SFTTrainer, SFTConfig
import torch

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name="unsloth/Qwen2.5-1.5B-Instruct",
    max_seq_length=1024,
    load_in_4bit=True,
)
model = FastLanguageModel.get_peft_model(
    model, r=16, lora_alpha=16,
    target_modules=["q_proj", "k_proj", "v_proj", "o_proj",
                    "gate_proj", "up_proj", "down_proj"],
    lora_dropout=0, bias="none",
    use_gradient_checkpointing="unsloth",
    random_state=3407,
)

date = load_dataset("json", data_files={"train": "train_v2.jsonl", "validation": "val_v2.jsonl"})

def formateaza(ex):
    return {"text": tokenizer.apply_chat_template(ex["messages"], tokenize=False)}

date = date.map(formateaza, remove_columns=["messages"])

trainer = SFTTrainer(
    model=model,
    processing_class=tokenizer,
    train_dataset=date["train"],
    eval_dataset=date["validation"],
    args=SFTConfig(
        dataset_text_field="text",
        per_device_train_batch_size=2,
        gradient_accumulation_steps=4,
        num_train_epochs=5,
        learning_rate=2e-4,
        warmup_steps=2,
        lr_scheduler_type="linear",
        optim="adamw_8bit",
        fp16=not is_bfloat16_supported(),
        bf16=is_bfloat16_supported(),
        logging_steps=1,
        eval_strategy="steps",
        eval_steps=5,
        save_strategy="no",
        report_to="none",
        output_dir="outputs",
        seed=3407,
    ),
)

trainer = train_on_responses_only(
    trainer,
    instruction_part="<|im_start|>user\n",
    response_part="<|im_start|>assistant\n",
)

trainer.train()

print("\nVRAM maxim în antrenare:", round(torch.cuda.max_memory_allocated() / 1e9, 2), "GB")

model.save_pretrained("adaptor_v2")
tokenizer.save_pretrained("adaptor_v2")
