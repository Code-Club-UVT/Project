# Experiment (toy data): 60 templated examples, 2 bug types, 3 fixed tutor answers per type.
# Written to train.jsonl (50) and val.jsonl (10).
# All Romanian strings (system prompt, names used inside the student code, complaints,
# tutor answers) are kept verbatim: they are the data the toy adapter was trained on.
import json
import random

random.seed(0)

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

function_names = ["suma", "total_lista", "aduna", "calculeaza_suma"]
list_names = ["lista", "numere", "valori", "note"]
accumulator_names = ["total", "rezultat", "s", "acumulat"]

def program_wrong_start(f, l, a):
    return (f"def {f}({l}):\n    {a} = 0\n    for i in range(1, len({l})):\n"
            f"        {a} += {l}[i]\n    return {a}")

def program_return_in_loop(f, l, a):
    return (f"def {f}({l}):\n    {a} = 0\n    for x in {l}:\n"
            f"        {a} += x\n        return {a}")

complaints_start = [
    "Funcția mea dă un rezultat prea mic.",
    "Nu înțeleg de ce suma nu iese corect.",
    "Rezultatul funcției e greșit, parcă lipsește ceva.",
]
answers_start = [
    "Hai să urmărim codul pas cu pas. Ce valoare are `i` la prima trecere prin buclă? Și ce element din listă corespunde acelei valori?",
    "Să luăm o listă mică, de exemplu cu trei elemente, și să rulăm funcția pe hârtie. Ce valori ia `i` pe parcurs? Sunt toate pozițiile din listă acoperite?",
    "Uită-te la `range(...)` din buclă. De la ce număr pornește și unde se oprește? Ce poziție are primul element al unei liste?",
]
complaints_return = [
    "Funcția mea returnează doar primul număr din listă.",
    "Suma iese mereu cât primul element, nu știu de ce.",
    "Funcția se oprește prea devreme, parcă.",
]
answers_return = [
    "Hai să urmărim execuția. Ce se întâmplă când Python ajunge la `return`? În ce moment al buclei se întâmplă asta?",
    "Rulează funcția pe hârtie cu o listă de trei elemente. Câte treceri prin buclă apucă să facă funcția înainte să se termine? Ce o oprește?",
    "Uită-te la indentarea lui `return`. Din ce bloc face parte? Ar trebui să se execute la fiecare trecere sau o singură dată?",
]

def example(code, complaint, answer):
    return {"messages": [
        {"role": "system", "content": SYSTEM},
        {"role": "user", "content": complaint + "\n\n" + code},
        {"role": "assistant", "content": answer},
    ]}

seen = set()
examples = []
while len(examples) < 60:
    f = random.choice(function_names)
    l = random.choice(list_names)
    a = random.choice(accumulator_names)
    if len(examples) % 2 == 0:
        code = program_wrong_start(f, l, a)
        ex = (code, random.choice(complaints_start), random.choice(answers_start))
    else:
        code = program_return_in_loop(f, l, a)
        ex = (code, random.choice(complaints_return), random.choice(answers_return))
    if code in seen:
        continue
    seen.add(code)
    examples.append(example(*ex))

random.shuffle(examples)

def write(name, rows):
    with open(name, "w", encoding="utf-8") as file:
        for e in rows:
            file.write(json.dumps(e, ensure_ascii=False) + "\n")

write("train.jsonl", examples[:50])
write("val.jsonl", examples[50:])
