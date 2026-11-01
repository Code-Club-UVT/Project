import json
import os
import random
import re
import sys
from collections import Counter
import requests

MODEL = "gemma3:12b"
URL = "http://localhost:11434/api/chat"
BRUT = "date/raspunsuri_brute.jsonl"
random.seed(0)

SYSTEM_TUTORE = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."
PLANGERI = [
    "Funcția mea nu merge, nu înțeleg de ce.",
    "Ceva e greșit în codul meu, mă poți ajuta?",
    "Rezultatul nu e cel pe care îl așteptam.",
    "Nu știu ce am greșit aici.",
]
DESCRIERE = {
    "comparatie_inversata": "o comparație a fost inversată",
    "interval_scurtat": "bucla se oprește cu un pas prea devreme",
    "valoare_initiala_gresita": "o variabilă primește o valoare de pornire greșită",
    "initializare_stearsa": "o variabilă este folosită fără să fi primit o valoare de pornire",
    "rest_gresit": "testul cu restul împărțirii verifică exact opusul",
    "return_in_bucla": "instrucțiunea return este în interiorul buclei, deci funcția se oprește prea devreme",
    "actualizare_stearsa": "variabila din condiția buclei while nu se mai schimbă, deci bucla nu se termină",
}

STILURI = [
    "Întreabă-l ce rezultat se așteaptă să primească pentru un exemplu concret de intrare, ales de tine. Începe cu „Dacă”.",
    "Cere-i să urmărească pas cu pas cum se schimbă valoarea unei variabile. Începe cu „Cum”.",
    "Întreabă-l ce se întâmplă la prima sau la ultima trecere prin buclă. Începe cu „La prima” sau „La ultima”.",
    "Întreabă-l ce rol are o anumită linie sau variabilă în program. Începe cu „Ce crezi”.",
    "Întreabă-l ce ar vedea dacă ar afișa cu print o anumită valoare. Începe cu „Dacă ai pune un print”.",
    "Întreabă-l de ce a ales o anumită valoare sau o anumită linie. Începe cu „De ce”.",
]
INCEPUTURI_INTERZISE = ("observă", "observi", "uită-te")

def cerere_profesor(m):
    stil = random.Random(m["id"]).choice(STILURI)
    if m["linie_modificata"] == "(linia a fost ștearsă)":
        ce = f"Linia `{m['linie_originala'].strip()}` a fost ștearsă (era linia {m['linie_bug']})."
    else:
        ce = f"Pe linia {m['linie_bug']}, `{m['linie_originala'].strip()}` a fost înlocuită cu `{m['linie_modificata'].strip()}`."
    return (
        "Un elev începător a scris codul de mai jos și are o greșeală.\n\n"
        f"```python\n{m['cod_cu_bug']}\n```\n\n"
        f"Ce e greșit (doar pentru tine, elevul nu știe): {DESCRIERE[m['categorie']]}. {ce}\n\n"
        "Scrie o singură întrebare scurtă, de cel mult două propoziții, pe care i-ai pune-o elevului "
        "ca să descopere singur greșeala. Nu spune care e greșeala, nu da soluția, nu scrie cod și nu cita "
        "varianta corectă. Poți numi variabila sau linia la care să se uite. "
        f"{stil} Nu începe cu „Observă”, „Observi” sau „Uită-te”. "
        "Scrie în limba română corectă, cu diacritice. Răspunde doar cu întrebarea."
    )

def intreaba_variat(text):
    for _ in range(3):
        r = intreaba(text)
        if not r.lower().startswith(INCEPUTURI_INTERZISE):
            return r
    return r

def intreaba(text):
    r = requests.post(URL, timeout=300, json={
        "model": MODEL, "stream": False,
        "messages": [{"role": "user", "content": text}],
        "options": {"temperature": 0.7, "num_predict": 150},
    })
    r.raise_for_status()
    return r.json()["message"]["content"].strip().strip('"„”').strip()

def conditie(linie):
    c = re.match(r"\s*(?:if|elif|while) (.*):", linie)
    return c.group(1) if c else None

def motiv_respingere(m, r):
    if len(r) > 350:
        return "prea lung"
    if "?" not in r:
        return "fără întrebare"
    if "```" in r:
        return "conține cod"
    orig = m["linie_originala"].strip()
    cat = m["categorie"]
    if cat != "return_in_bucla" and orig in r:
        return "dă soluția"
    c_orig = conditie(m["linie_originala"])
    if cat in ("comparatie_inversata", "rest_gresit") and c_orig and c_orig in r:
        return "dă soluția"
    ancore = [rf"linia {m['linie_bug']}\b"]
    v = re.match(r"(\w+) ", orig)
    if cat in ("valoare_initiala_gresita", "initializare_stearsa", "actualizare_stearsa") and v:
        ancore.append(rf"\b{re.escape(v.group(1))}\b")
    elif cat == "return_in_bucla":
        ancore.append("return")
    elif cat == "interval_scurtat":
        ancore += ["range", "ultim"]
    elif cat == "comparatie_inversata":
        c = conditie(m["linie_modificata"])
        if c:
            ancore.append(re.escape(c))
        ancore += ["condiți", "compar"]
    elif cat == "rest_gresit":
        ancore += ["%", "rest", r"\bpar(e|i)?\b"]
    if not any(re.search(a, r, re.IGNORECASE) for a in ancore):
        return "nu arată spre greșeală"
    return None

try:
    requests.get("http://localhost:11434", timeout=5)
except requests.exceptions.ConnectionError:
    sys.exit("Ollama nu rulează. Deschide altă fereastră Ubuntu, scrie `ollama serve` și reîncearcă.")

with open("date/mutanti.jsonl", encoding="utf-8") as f:
    mutanti = [json.loads(l) for l in f]

facute = {}
if os.path.exists(BRUT):
    with open(BRUT, encoding="utf-8") as f:
        for l in f:
            x = json.loads(l)
            facute[x["id"]] = x["raspuns"]

with open(BRUT, "a", encoding="utf-8") as f:
    for n, m in enumerate(mutanti, 1):
        if m["id"] in facute:
            continue
        r = intreaba_variat(cerere_profesor(m))
        facute[m["id"]] = r
        f.write(json.dumps({"id": m["id"], "raspuns": r}, ensure_ascii=False) + "\n")
        f.flush()
        if n % 25 == 0:
            print(f"{n}/{len(mutanti)} gata", flush=True)

pastrate, motive = [], Counter()
for m in mutanti:
    r = facute[m["id"]]
    motiv = motiv_respingere(m, r)
    if motiv:
        motive[motiv] += 1
        continue
    pastrate.append((m, r))

programe = sorted({m["task_id"] for m, _ in pastrate})
random.shuffle(programe)
n = len(programe)
test_ids = set(programe[: n // 10])
val_ids = set(programe[n // 10: n // 5])

grupe = {"train": [], "val": [], "test": []}
for m, r in pastrate:
    g = "test" if m["task_id"] in test_ids else "val" if m["task_id"] in val_ids else "train"
    grupe[g].append({
        "messages": [
            {"role": "system", "content": SYSTEM_TUTORE},
            {"role": "user", "content": random.choice(PLANGERI) + "\n\n" + m["cod_cu_bug"]},
            {"role": "assistant", "content": r},
        ],
        **{k: m[k] for k in ("id", "task_id", "categorie", "linie_bug", "linie_originala", "linie_modificata")},
    })

for g, lista in grupe.items():
    random.shuffle(lista)
    with open(f"date/{g}.jsonl", "w", encoding="utf-8") as f:
        for e in lista:
            f.write(json.dumps(e, ensure_ascii=False) + "\n")

print("\nGenerate:", len(mutanti), "| păstrate:", len(pastrate), "| respinse:", sum(motive.values()))
for motiv, c in motive.most_common():
    print("   respinse,", motiv + ":", c)
print("Păstrate pe categorii:", dict(Counter(m["categorie"] for m, _ in pastrate)))
print("Antrenare:", len(grupe["train"]), "| validare:", len(grupe["val"]), "| test:", len(grupe["test"]))
print("\n--- 6 exemple la întâmplare ---")
for e in random.sample(grupe["train"], min(6, len(grupe["train"]))):
    print(f"\n[{e['categorie']}] linia {e['linie_bug']}: {e['linie_modificata'].strip()}")
    print("TUTORE:", e["messages"][2]["content"])
