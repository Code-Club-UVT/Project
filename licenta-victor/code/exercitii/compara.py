import torch
from unsloth import FastLanguageModel

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

teste = [
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

def ruleaza(nume_model):
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=nume_model, max_seq_length=1024, load_in_4bit=True)
    FastLanguageModel.for_inference(model)
    raspunsuri = []
    for plangere, cod in teste:
        mesaje = [
            {"role": "system", "content": SYSTEM},
            {"role": "user", "content": plangere + "\n\n" + cod},
        ]
        intrare = tokenizer.apply_chat_template(
            mesaje, add_generation_prompt=True, return_tensors="pt", return_dict=True
        ).to("cuda")
        iesire = model.generate(**intrare, max_new_tokens=150, do_sample=False)
        raspunsuri.append(tokenizer.decode(
            iesire[0][intrare["input_ids"].shape[1]:], skip_special_tokens=True))
    del model
    torch.cuda.empty_cache()
    return raspunsuri

baza = ruleaza("unsloth/Qwen2.5-1.5B-Instruct")
antrenat = ruleaza("adaptor_jucarie")

with open("comparatie.txt", "w", encoding="utf-8") as f:
    for i, (plangere, cod) in enumerate(teste):
        f.write(f"\n===== TEST {i+1}: {plangere}\n{cod}\n\n")
        f.write(f"--- MODEL ORIGINAL ---\n{baza[i]}\n\n")
        f.write(f"--- MODEL ANTRENAT ---\n{antrenat[i]}\n")

def numara(lista, semn):
    return sum(semn in r for r in lista)

print("\nRăspunsuri cu bloc de cod:  original", numara(baza, "```"), "/ 8 | antrenat", numara(antrenat, "```"), "/ 8")
print("Răspunsuri cu semn de întrebare: original", numara(baza, "?"), "/ 8 | antrenat", numara(antrenat, "?"), "/ 8")
