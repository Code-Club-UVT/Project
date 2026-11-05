# Experiment: load Qwen2.5-1.5B-Instruct in 4 bits, ask one question, measure VRAM and
# compare token counts for the same sentence in English and Romanian.
# The Romanian prompt is kept verbatim: it is the input the recorded results came from.
from unsloth import FastLanguageModel
import torch

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name="unsloth/Qwen2.5-1.5B-Instruct",
    max_seq_length=1024,
    load_in_4bit=True,
)
FastLanguageModel.for_inference(model)

messages = [{"role": "user", "content": "Ce este o buclă for în Python? Răspunde în două propoziții."}]
inputs = tokenizer.apply_chat_template(
    messages, add_generation_prompt=True, return_tensors="pt", return_dict=True
).to("cuda")

output = model.generate(**inputs, max_new_tokens=100)
answer = tokenizer.decode(output[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True)

print("\nANSWER:", answer)
print("Tokens in the question:", inputs["input_ids"].shape[1])
print("Peak VRAM used:", round(torch.cuda.max_memory_allocated() / 1e9, 2), "GB")

en = len(tokenizer("A for loop repeats a block of code once for each item in a list.")["input_ids"])
ro = len(tokenizer("O buclă for repetă un bloc de cod pentru fiecare element dintr-o listă.")["input_ids"])
print("Same sentence, tokens in English:", en, "| in Romanian:", ro)
