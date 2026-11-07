# Experiment: base Qwen2.5-1.5B vs the toy adapter on 8 hand-written bugs.
# Writes comparison.txt. The Romanian prompts and test programs are kept verbatim:
# they are the inputs the recorded answers in comparison.txt came from.
import torch
from unsloth import FastLanguageModel

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

tests = [
    ("Funcția maxim îmi returnează cel mai mic număr din listă.",
     "def maxim(lista):\n    m = lista[0]\n    for x in lista:\n        if x < m:\n            m = x\n    return m"),
    ("Primesc o eroare când rulez funcția.",
     "def suma_pozitive(numere):\n    for x in numere:\n        if x > 0:\n            total += x\n    return total"),
    ("Funcția nu afișează ultimul element al listei.",
     "def afiseaza(lista):\n    for i in range(len(lista) - 1):\n        print(lista[i])"),
    ("Funcția mea numără greșit numerele pare.",
     "def numara_pare(lista):\n    contor = 0\n    for x in lista:\n        if x % 2 == 1:\n            contor += 1\n    return contor"),
    ("Programul meu nu se oprește niciodată.",
     "def numara_invers(n):\n    while n > 0:\n        print(n)\n    print(\"Gata\")"),
    ("Primesc IndexError când apelez funcția.",
     "def ultimul(lista):\n    return lista[len(lista)]"),
    ("Funcția produs returnează mereu 0.",
     "def produs(lista):\n    rezultat = 0\n    for x in lista:\n        rezultat *= x\n    return rezultat"),
    ("Funcția suma returnează doar ultimul element.",
     "def suma(lista):\n    for x in lista:\n        total = 0\n        total += x\n    return total"),
]

def run(model_name):
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=model_name, max_seq_length=1024, load_in_4bit=True)
    FastLanguageModel.for_inference(model)
    answers = []
    for complaint, code in tests:
        messages = [
            {"role": "system", "content": SYSTEM},
            {"role": "user", "content": complaint + "\n\n" + code},
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

base = run("unsloth/Qwen2.5-1.5B-Instruct")
trained = run("adapter_toy")

with open("comparison.txt", "w", encoding="utf-8") as f:
    for i, (complaint, code) in enumerate(tests):
        f.write(f"\n===== TEST {i+1}: {complaint}\n{code}\n\n")
        f.write(f"--- BASE MODEL ---\n{base[i]}\n\n")
        f.write(f"--- TRAINED MODEL ---\n{trained[i]}\n")

def count(answers, mark):
    return sum(mark in r for r in answers)

print("\nAnswers with a code block:   base", count(base, "```"), "/ 8 | trained", count(trained, "```"), "/ 8")
print("Answers with a question mark: base", count(base, "?"), "/ 8 | trained", count(trained, "?"), "/ 8")
