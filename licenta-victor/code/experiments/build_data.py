# Experiment: build data v2 from mutants.jsonl: 3 templated tutor answers per mutant,
# split by base program (val: media; test: maxim, numara_pare, cauta).
# The Romanian system prompt, complaints and answer templates are kept verbatim: they are the data.
import json
import random
import re
from collections import Counter

random.seed(0)

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

TEST = {"maxim", "numara_pare", "cauta"}
VAL = {"media"}

complaints = [
    "Funcția mea nu merge, nu înțeleg de ce.",
    "Ceva e greșit în codul meu, mă poți ajuta?",
    "Rezultatul nu e cel pe care îl așteptam.",
    "Nu știu ce am greșit aici.",
]

answers = {
    "wrong_initial_value": [
        "Ia o listă cu un singur element și rulează funcția pe hârtie. Ce ar trebui să returneze? Urmărește valoarea lui `{var}` de la început până la `return`: ce obții?",
        "Uită-te la linia `{line}`. Ce valoare ar trebui să aibă `{var}` înainte ca bucla să înceapă să lucreze? De ce?",
        "Compară ce returnează funcția cu ce te-ai aștepta pe un exemplu mic. Ce se întâmplă cu `{var}` înainte de prima trecere prin buclă?",
    ],
    "deleted_initialization": [
        "Rulează funcția pe hârtie, pas cu pas. La prima dată când `{var}` e folosită, are deja o valoare? De unde o are?",
        "Unde îi dai prima valoare lui `{var}` în funcție? Se întâmplă înainte să o folosești?",
        "Urmărește `{var}` de la începutul funcției. Care e prima linie în care apare? Ce valoare are în acel moment?",
    ],
    "return_in_loop": [
        "Urmărește execuția: câte treceri prin buclă face funcția înainte să ajungă la `return`? Ce o oprește?",
        "Uită-te la indentarea lui `return`. Din ce bloc face parte? Ar trebui să se execute la fiecare trecere sau o singură dată?",
        "Rulează funcția pe hârtie cu o listă de trei elemente. Ce se întâmplă după primul element?",
    ],
    "flipped_comparison": [
        "Alege un exemplu mic și urmărește condiția `{cond}`. Pentru ce valori e adevărată? Asta vrei să se întâmple?",
        "Spune cu cuvintele tale ce verifică `{cond}`. Corespunde cu ce vrei să facă funcția?",
        "Rulează funcția pe un exemplu mic și, la fiecare trecere, notează dacă `{cond}` e adevărată sau falsă. Ce observi?",
    ],
    "wrong_remainder": [
        "Ce rest dă un număr par la împărțirea cu 2? Dar unul impar? Compară cu ce testează `{cond}`.",
        "Alege un număr par și unul impar și verifică pe hârtie, pentru fiecare, dacă `{cond}` e adevărată. Pentru care intră funcția în `if`?",
        "Ce fel de numere vrei să selectezi cu `if`? Selectează `{cond}` exact aceste numere?",
    ],
    "shortened_range": [
        "Ia o listă cu trei elemente. Ce valori ia `i` în `{rng}`? Se ajunge la ultimul element?",
        "Uită-te la `{rng}`. Care e ultima valoare pe care o ia `i`? Ce poziție are ultimul element al listei?",
        "Numără câte treceri face bucla pentru o listă cu 3 elemente. Câte ar trebui să fie?",
    ],
    "deleted_update": [
        "Cum își schimbă valoarea `{var}` de la o trecere prin buclă la alta? Când devine falsă condiția din `while`?",
        "Urmărește valoarea lui `{var}` pe hârtie, trecere cu trecere. Ajunge condiția din `while` să fie vreodată falsă?",
        "Ce ar trebui să se întâmple cu `{var}` în corpul buclei ca ciclul să se termine? Se întâmplă asta în cod?",
    ],
}

def extract(m):
    orig = m["original_line"].strip()
    mod = m["modified_line"].strip()
    v = re.match(r"(\w+) ", orig)
    c = re.match(r"(?:if|while) (.*):", mod)
    r = re.search(r"range\(.*\)", mod)
    return {
        "var": v.group(1) if v else "",
        "line": mod,
        "cond": c.group(1) if c else "",
        "rng": r.group(0) if r else "",
    }

def example(m, complaint, answer):
    return {"messages": [
        {"role": "system", "content": SYSTEM},
        {"role": "user", "content": complaint + "\n\n" + m["buggy_code"]},
        {"role": "assistant", "content": answer},
    ]}

with open("mutants.jsonl", encoding="utf-8") as f:
    mutants = [json.loads(line) for line in f]

train, val, test = [], [], []
prog_train, prog_val, prog_test = set(), set(), set()

for m in mutants:
    if m["program"] in TEST:
        prog_test.add(m["program"])
        test.append({
            "id": m["id"], "program": m["program"], "category": m["category"],
            "bug_line": m["bug_line"], "original_line": m["original_line"],
            "modified_line": m["modified_line"], "buggy_code": m["buggy_code"],
            "complaint": complaints[0],
        })
        continue
    if m["program"] in VAL:
        target = val
        prog_val.add(m["program"])
    else:
        target = train
        prog_train.add(m["program"])
    values = extract(m)
    for template in answers[m["category"]]:
        answer = template.format(**values)
        assert m["original_line"].strip() not in answer, "Solution leaked: " + answer
        target.append(example(m, random.choice(complaints), answer))

assert not (prog_train & prog_val), "Leak between train and validation"
assert not (prog_train & prog_test), "Leak between train and test"
assert not (prog_val & prog_test), "Leak between validation and test"

random.shuffle(train)
random.shuffle(val)

def write(name, rows):
    with open(name, "w", encoding="utf-8") as f:
        for e in rows:
            f.write(json.dumps(e, ensure_ascii=False) + "\n")

write("train_v2.jsonl", train)
write("val_v2.jsonl", val)
write("test_v2.jsonl", test)

print("Examples: train", len(train), "| val", len(val), "| test", len(test))
print("Base programs in train:", sorted(prog_train))
print("Base programs in val:  ", sorted(prog_val))
print("Base programs in test: ", sorted(prog_test))
print("Categories in test:", dict(Counter(t["category"] for t in test)))
print("Checks passed: no data leak, no answer contains the solution.")

e = train[0]["messages"]
print("\n--- One training example ---")
print(e[1]["content"])
print("\nTUTOR ANSWER:", e[2]["content"])
