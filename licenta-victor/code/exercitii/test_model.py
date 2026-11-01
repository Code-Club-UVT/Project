from unsloth import FastLanguageModel
import torch

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name="unsloth/Qwen2.5-1.5B-Instruct",
    max_seq_length=1024,
    load_in_4bit=True,
)
FastLanguageModel.for_inference(model)

mesaje = [{"role": "user", "content": "Ce este o buclă for în Python? Răspunde în două propoziții."}]
intrare = tokenizer.apply_chat_template(
    mesaje, add_generation_prompt=True, return_tensors="pt", return_dict=True
).to("cuda")

iesire = model.generate(**intrare, max_new_tokens=100)
raspuns = tokenizer.decode(iesire[0][intrare["input_ids"].shape[1]:], skip_special_tokens=True)

print("\nRĂSPUNS:", raspuns)
print("Tokeni în întrebare:", intrare["input_ids"].shape[1])
print("VRAM maxim folosit:", round(torch.cuda.max_memory_allocated() / 1e9, 2), "GB")

en = len(tokenizer("A for loop repeats a block of code once for each item in a list.")["input_ids"])
ro = len(tokenizer("O buclă for repetă un bloc de cod pentru fiecare element dintr-o listă.")["input_ids"])
print("Aceeași propoziție, tokeni în engleză:", en, "| în română:", ro)
