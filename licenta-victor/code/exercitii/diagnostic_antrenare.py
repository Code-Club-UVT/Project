import json
import re
from collections import defaultdict
from unsloth import FastLanguageModel

with open("mutanti.jsonl", encoding="utf-8") as f:
    mutanti = [json.loads(linie) for linie in f]

with open("train_v2.jsonl", encoding="utf-8") as f:
    exemple = [json.loads(linie) for linie in f]

def gaseste(user):
    for m in mutanti:
        if m["cod_cu_bug"] in user:
            return m
    return None

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

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name="adaptor_v2", max_seq_length=1024, load_in_4bit=True)
FastLanguageModel.for_inference(model)

rez = []
for e in exemple:
    mesaje = e["messages"][:2]
    t = gaseste(mesaje[1]["content"])
    assert t is not None, "Nu am găsit eticheta pentru un exemplu"
    intrare = tokenizer.apply_chat_template(
        mesaje, add_generation_prompt=True, return_tensors="pt", return_dict=True
    ).to("cuda")
    iesire = model.generate(**intrare, max_new_tokens=150, do_sample=False)
    r = tokenizer.decode(iesire[0][intrare["input_ids"].shape[1]:], skip_special_tokens=True)
    rez.append((t, e["messages"][2]["content"], r))

def are_ancora(t, r):
    return any(re.search(x, r, re.IGNORECASE) for x in ancore(t))

DESCHIDERE = "Rulează funcția pe hârtie cu o listă"
pe_categorie = defaultdict(lambda: [0, 0])
for t, _, r in rez:
    pe_categorie[t["categorie"]][0] += are_ancora(t, r)
    pe_categorie[t["categorie"]][1] += 1

with open("diagnostic_antrenare.txt", "w", encoding="utf-8") as f:
    for t, tinta, r in rez:
        f.write(f"[{t['categorie']}] {t['linie_modificata'].strip()}\n")
        f.write(f"ȚINTA:   {tinta}\n")
        f.write(f"MODELUL: {r}\n\n")

total_a = sum(are_ancora(t, r) for t, _, r in rez)
print("\nMenționează elementul greșit, pe programele din antrenare:", total_a, "/", len(rez))
for c, (a, n) in sorted(pe_categorie.items()):
    print(f"  {c:<28}{a}/{n}")
print("Răspunsuri diferite între ele:", len(set(r for _, _, r in rez)), "din", len(rez))
print("Răspunsuri care încep cu «" + DESCHIDERE + "»:", sum(r.startswith(DESCHIDERE) for _, _, r in rez), "din", len(rez))
