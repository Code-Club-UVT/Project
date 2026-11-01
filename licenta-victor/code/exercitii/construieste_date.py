import json
import random
import re
from collections import Counter

random.seed(0)

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

TEST = {"maxim", "numara_pare", "cauta"}
VAL = {"media"}

plangeri = [
    "Funcția mea nu merge, nu înțeleg de ce.",
    "Ceva e greșit în codul meu, mă poți ajuta?",
    "Rezultatul nu e cel pe care îl așteptam.",
    "Nu știu ce am greșit aici.",
]

raspunsuri = {
    "valoare_initiala_gresita": [
        "Ia o listă cu un singur element și rulează funcția pe hârtie. Ce ar trebui să returneze? Urmărește valoarea lui `{var}` de la început până la `return`: ce obții?",
        "Uită-te la linia `{linie}`. Ce valoare ar trebui să aibă `{var}` înainte ca bucla să înceapă să lucreze? De ce?",
        "Compară ce returnează funcția cu ce te-ai aștepta pe un exemplu mic. Ce se întâmplă cu `{var}` înainte de prima trecere prin buclă?",
    ],
    "initializare_stearsa": [
        "Rulează funcția pe hârtie, pas cu pas. La prima dată când `{var}` e folosită, are deja o valoare? De unde o are?",
        "Unde îi dai prima valoare lui `{var}` în funcție? Se întâmplă înainte să o folosești?",
        "Urmărește `{var}` de la începutul funcției. Care e prima linie în care apare? Ce valoare are în acel moment?",
    ],
    "return_in_bucla": [
        "Urmărește execuția: câte treceri prin buclă face funcția înainte să ajungă la `return`? Ce o oprește?",
        "Uită-te la indentarea lui `return`. Din ce bloc face parte? Ar trebui să se execute la fiecare trecere sau o singură dată?",
        "Rulează funcția pe hârtie cu o listă de trei elemente. Ce se întâmplă după primul element?",
    ],
    "comparatie_inversata": [
        "Alege un exemplu mic și urmărește condiția `{cond}`. Pentru ce valori e adevărată? Asta vrei să se întâmple?",
        "Spune cu cuvintele tale ce verifică `{cond}`. Corespunde cu ce vrei să facă funcția?",
        "Rulează funcția pe un exemplu mic și, la fiecare trecere, notează dacă `{cond}` e adevărată sau falsă. Ce observi?",
    ],
    "rest_gresit": [
        "Ce rest dă un număr par la împărțirea cu 2? Dar unul impar? Compară cu ce testează `{cond}`.",
        "Alege un număr par și unul impar și verifică pe hârtie, pentru fiecare, dacă `{cond}` e adevărată. Pentru care intră funcția în `if`?",
        "Ce fel de numere vrei să selectezi cu `if`? Selectează `{cond}` exact aceste numere?",
    ],
    "interval_scurtat": [
        "Ia o listă cu trei elemente. Ce valori ia `i` în `{rng}`? Se ajunge la ultimul element?",
        "Uită-te la `{rng}`. Care e ultima valoare pe care o ia `i`? Ce poziție are ultimul element al listei?",
        "Numără câte treceri face bucla pentru o listă cu 3 elemente. Câte ar trebui să fie?",
    ],
    "actualizare_stearsa": [
        "Cum își schimbă valoarea `{var}` de la o trecere prin buclă la alta? Când devine falsă condiția din `while`?",
        "Urmărește valoarea lui `{var}` pe hârtie, trecere cu trecere. Ajunge condiția din `while` să fie vreodată falsă?",
        "Ce ar trebui să se întâmple cu `{var}` în corpul buclei ca ciclul să se termine? Se întâmplă asta în cod?",
    ],
}

def extrage(m):
    orig = m["linie_originala"].strip()
    mod = m["linie_modificata"].strip()
    v = re.match(r"(\w+) ", orig)
    c = re.match(r"(?:if|while) (.*):", mod)
    r = re.search(r"range\(.*\)", mod)
    return {
        "var": v.group(1) if v else "",
        "linie": mod,
        "cond": c.group(1) if c else "",
        "rng": r.group(0) if r else "",
    }

def exemplu(m, plangere, raspuns):
    return {"messages": [
        {"role": "system", "content": SYSTEM},
        {"role": "user", "content": plangere + "\n\n" + m["cod_cu_bug"]},
        {"role": "assistant", "content": raspuns},
    ]}

with open("mutanti.jsonl", encoding="utf-8") as f:
    mutanti = [json.loads(linie) for linie in f]

train, val, test = [], [], []
prog_train, prog_val, prog_test = set(), set(), set()

for m in mutanti:
    if m["program"] in TEST:
        prog_test.add(m["program"])
        test.append({
            "id": m["id"], "program": m["program"], "categorie": m["categorie"],
            "linie_bug": m["linie_bug"], "linie_originala": m["linie_originala"],
            "linie_modificata": m["linie_modificata"], "cod_cu_bug": m["cod_cu_bug"],
            "plangere": plangeri[0],
        })
        continue
    if m["program"] in VAL:
        camp = val
        prog_val.add(m["program"])
    else:
        camp = train
        prog_train.add(m["program"])
    valori = extrage(m)
    for sablon in raspunsuri[m["categorie"]]:
        raspuns = sablon.format(**valori)
        assert m["linie_originala"].strip() not in raspuns, "Soluție scăpată: " + raspuns
        camp.append(exemplu(m, random.choice(plangeri), raspuns))

assert not (prog_train & prog_val), "Scurgere între antrenare și validare"
assert not (prog_train & prog_test), "Scurgere între antrenare și test"
assert not (prog_val & prog_test), "Scurgere între validare și test"

random.shuffle(train)
random.shuffle(val)

def scrie(nume, lista):
    with open(nume, "w", encoding="utf-8") as f:
        for e in lista:
            f.write(json.dumps(e, ensure_ascii=False) + "\n")

scrie("train_v2.jsonl", train)
scrie("val_v2.jsonl", val)
scrie("test_v2.jsonl", test)

print("Exemple: antrenare", len(train), "| validare", len(val), "| test", len(test))
print("Programe de bază în antrenare:", sorted(prog_train))
print("Programe de bază în validare: ", sorted(prog_val))
print("Programe de bază în test:     ", sorted(prog_test))
print("Categorii în test:", dict(Counter(t["categorie"] for t in test)))
print("Verificări: fără scurgere de date, niciun răspuns nu conține soluția.")

e = train[0]["messages"]
print("\n--- Un exemplu de antrenare ---")
print(e[1]["content"])
print("\nRĂSPUNS TUTORE:", e[2]["content"])
