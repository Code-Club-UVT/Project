import json
import random

random.seed(0)

SYSTEM = "Ești un tutore de programare. Nu da niciodată soluția. Pune întrebări care îl ghidează pe elev."

functii = ["suma", "total_lista", "aduna", "calculeaza_suma"]
liste = ["lista", "numere", "valori", "note"]
acumulatori = ["total", "rezultat", "s", "acumulat"]

def program_start_gresit(f, l, a):
    return (f"def {f}({l}):\n    {a} = 0\n    for i in range(1, len({l})):\n"
            f"        {a} += {l}[i]\n    return {a}")

def program_return_in_bucla(f, l, a):
    return (f"def {f}({l}):\n    {a} = 0\n    for x in {l}:\n"
            f"        {a} += x\n        return {a}")

plangeri_start = [
    "Funcția mea dă un rezultat prea mic.",
    "Nu înțeleg de ce suma nu iese corect.",
    "Rezultatul funcției e greșit, parcă lipsește ceva.",
]
raspunsuri_start = [
    "Hai să urmărim codul pas cu pas. Ce valoare are `i` la prima trecere prin buclă? Și ce element din listă corespunde acelei valori?",
    "Să luăm o listă mică, de exemplu cu trei elemente, și să rulăm funcția pe hârtie. Ce valori ia `i` pe parcurs? Sunt toate pozițiile din listă acoperite?",
    "Uită-te la `range(...)` din buclă. De la ce număr pornește și unde se oprește? Ce poziție are primul element al unei liste?",
]
plangeri_return = [
    "Funcția mea returnează doar primul număr din listă.",
    "Suma iese mereu cât primul element, nu știu de ce.",
    "Funcția se oprește prea devreme, parcă.",
]
raspunsuri_return = [
    "Hai să urmărim execuția. Ce se întâmplă când Python ajunge la `return`? În ce moment al buclei se întâmplă asta?",
    "Rulează funcția pe hârtie cu o listă de trei elemente. Câte treceri prin buclă apucă să facă funcția înainte să se termine? Ce o oprește?",
    "Uită-te la indentarea lui `return`. Din ce bloc face parte? Ar trebui să se execute la fiecare trecere sau o singură dată?",
]

def exemplu(cod, plangere, raspuns):
    return {"messages": [
        {"role": "system", "content": SYSTEM},
        {"role": "user", "content": plangere + "\n\n" + cod},
        {"role": "assistant", "content": raspuns},
    ]}

vazute = set()
exemple = []
while len(exemple) < 60:
    f = random.choice(functii)
    l = random.choice(liste)
    a = random.choice(acumulatori)
    if len(exemple) % 2 == 0:
        cod = program_start_gresit(f, l, a)
        ex = (cod, random.choice(plangeri_start), random.choice(raspunsuri_start))
    else:
        cod = program_return_in_bucla(f, l, a)
        ex = (cod, random.choice(plangeri_return), random.choice(raspunsuri_return))
    if cod in vazute:
        continue
    vazute.add(cod)
    exemple.append(exemplu(*ex))

random.shuffle(exemple)

def scrie(nume, lista):
    with open(nume, "w", encoding="utf-8") as fisier:
        for e in lista:
            fisier.write(json.dumps(e, ensure_ascii=False) + "\n")

scrie("train.jsonl", exemple[:50])
scrie("val.jsonl", exemple[50:])
