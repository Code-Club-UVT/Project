import json
import re
import torch
from unsloth import FastLanguageModel

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

with open("test_v2.jsonl", encoding="utf-8") as f:
    teste = [json.loads(linie) for linie in f]

modele = [
    ("original", "unsloth/Qwen2.5-1.5B-Instruct"),
    ("jucarie", "adaptor_jucarie"),
    ("v2", "adaptor_v2"),
]

def ancore(t):
    c = t["categorie"]
    orig = t["linie_originala"].strip()
    mod = t["linie_modificata"].strip()
    var = re.match(r"(\w+) ", orig)
    cond = re.match(r"(?:if|while) (.*):", mod)
    if c in ("valoare_initiala_gresita", "initializare_stearsa", "actualizare_stearsa"):
        return [r"\b" + var.group(1) + r"\b"] if var else []
    if c == "return_in_bucla":
        return ["return"]
    if c == "comparatie_inversata":
        return [re.escape(cond.group(1))] if cond else []
    if c == "rest_gresit":
        return [re.escape(cond.group(1)), "%", "rest"] if cond else ["%"]
    if c == "interval_scurtat":
        return ["range"]
    return []

def ruleaza(cale):
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=cale, max_seq_length=1024, load_in_4bit=True)
    FastLanguageModel.for_inference(model)
    raspunsuri = []
    for t in teste:
        mesaje = [
            {"role": "system", "content": SYSTEM},
            {"role": "user", "content": t["plangere"] + "\n\n" + t["cod_cu_bug"]},
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

rezultate = {nume: ruleaza(cale) for nume, cale in modele}

def verifica(t, r):
    ancora = any(re.search(a, r, re.IGNORECASE) for a in ancore(t))
    cod = "```" in r
    scurs = t["linie_originala"].strip() in r
    return ancora, cod, scurs

with open("comparatie_v2.txt", "w", encoding="utf-8") as f:
    for i, t in enumerate(teste):
        f.write(f"\n===== TEST {i+1} ({t['categorie']}, linia {t['linie_bug']}): {t['linie_modificata'].strip()}\n")
        f.write(t["cod_cu_bug"] + "\n")
        for nume, _ in modele:
            f.write(f"\n--- {nume.upper()} ---\n{rezultate[nume][i]}\n")

print("\nA = menționează elementul greșit | C = bloc de cod | S = conține linia corectă")
print(f"{'#':<4}{'categorie':<28}" + "".join(f"{nume:<10}" for nume, _ in modele))
for i, t in enumerate(teste):
    celule = ""
    for nume, _ in modele:
        a, c, s = verifica(t, rezultate[nume][i])
        celule += f"{'A' if a else '-'}{'C' if c else '-'}{'S' if s else '-'}".ljust(10)
    print(f"{i+1:<4}{t['categorie']:<28}" + celule)

print()
for nume, _ in modele:
    v = [verifica(t, r) for t, r in zip(teste, rezultate[nume])]
    print(f"{nume:<9} menționează elementul: {sum(x[0] for x in v)}/9 | bloc de cod: {sum(x[1] for x in v)}/9 | linia corectă: {sum(x[2] for x in v)}/9")
