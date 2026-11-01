import json
import re
import subprocess
import sys
from collections import Counter

STERGE = "STERGE"

programe = [
    {"nume": "suma", "apel": "print(suma([3, 1, 4, 1, 5]))", "cod": """def suma(lista):
    total = 0
    for x in lista:
        total += x
    return total"""},
    {"nume": "maxim", "apel": "print(maxim([3, 9, 4, 1, 5]))", "cod": """def maxim(lista):
    m = lista[0]
    for x in lista:
        if x > m:
            m = x
    return m"""},
    {"nume": "numara_pare", "apel": "print(numara_pare([2, 4, 6, 7, 9]))", "cod": """def numara_pare(lista):
    contor = 0
    for x in lista:
        if x % 2 == 0:
            contor += 1
    return contor"""},
    {"nume": "produs", "apel": "print(produs([2, 3, 4]))", "cod": """def produs(lista):
    rezultat = 1
    for x in lista:
        rezultat *= x
    return rezultat"""},
    {"nume": "afiseaza", "apel": "afiseaza([10, 20, 30])", "cod": """def afiseaza(lista):
    for i in range(len(lista)):
        print(lista[i])"""},
    {"nume": "numara_invers", "apel": "numara_invers(3)", "cod": """def numara_invers(n):
    while n > 0:
        print(n)
        n -= 1
    print("Gata")"""},
    {"nume": "contor_pozitive", "apel": "print(contor_pozitive([3, -1, 4, -5, 2]))", "cod": """def contor_pozitive(lista):
    contor = 0
    for x in lista:
        if x > 0:
            contor += 1
    return contor"""},
    {"nume": "cauta", "apel": "print(cauta([5, 8, 2, 9], 9))", "cod": """def cauta(lista, x):
    for i in range(len(lista)):
        if lista[i] == x:
            return i
    return -1"""},
    {"nume": "media", "apel": "print(media([2, 4, 6]))", "cod": """def media(lista):
    total = 0
    for x in lista:
        total += x
    return total / len(lista)"""},
    {"nume": "suma_pare", "apel": "print(suma_pare([2, 3, 4, 5, 6]))", "cod": """def suma_pare(lista):
    total = 0
    for x in lista:
        if x % 2 == 0:
            total += x
    return total"""},
]

def comparatie_inversata(linii, i):
    l = linii[i]
    if re.match(r"\s*(if|while) ", l):
        if " > " in l:
            return l.replace(" > ", " < ", 1)
        if " < " in l:
            return l.replace(" < ", " > ", 1)
    return None

def interval_scurtat(linii, i):
    m = re.search(r"range\(len\((\w+)\)\)", linii[i])
    if m:
        return linii[i].replace(m.group(0), "range(len(" + m.group(1) + ") - 1)")
    return None

def valoare_initiala_gresita(linii, i):
    m = re.fullmatch(r"(\s+\w+ = )(0|1)", linii[i])
    if m:
        return m.group(1) + ("1" if m.group(2) == "0" else "0")
    return None

def initializare_stearsa(linii, i):
    if re.fullmatch(r"\s+\w+ = (0|1|\w+\[0\])", linii[i]):
        return STERGE
    return None

def rest_gresit(linii, i):
    l = linii[i]
    if "%" in l and "== 0" in l:
        return l.replace("== 0", "== 1")
    return None

def return_in_bucla(linii, i):
    l = linii[i]
    if l.startswith("    return ") and i > 0 and linii[i - 1].startswith("        "):
        return "    " + l
    return None

def actualizare_stearsa(linii, i):
    if re.fullmatch(r"\s+\w+ [-+]= 1", linii[i]) and any(
            x.strip().startswith("while ") for x in linii[:i]):
        return STERGE
    return None

MUTATII = [
    ("comparatie_inversata", comparatie_inversata),
    ("interval_scurtat", interval_scurtat),
    ("valoare_initiala_gresita", valoare_initiala_gresita),
    ("initializare_stearsa", initializare_stearsa),
    ("rest_gresit", rest_gresit),
    ("return_in_bucla", return_in_bucla),
    ("actualizare_stearsa", actualizare_stearsa),
]

def ruleaza(cod):
    try:
        r = subprocess.run([sys.executable, "-c", cod],
                           capture_output=True, text=True, timeout=1)
        return (r.returncode, r.stdout, r.stderr)
    except subprocess.TimeoutExpired:
        return ("timeout", "", "")

mutanti = []
respinse = 0

for p in programe:
    linii = p["cod"].split("\n")
    referinta = ruleaza(p["cod"] + "\n" + p["apel"])
    assert referinta[0] == 0, "Programul corect nu rulează: " + p["nume"]
    vazute = set()
    for i in range(len(linii)):
        for categorie, functie in MUTATII:
            nou = functie(linii, i)
            if nou is None:
                continue
            if nou == STERGE:
                linii_noi = linii[:i] + linii[i + 1:]
                modificata = "(linia a fost ștearsă)"
            else:
                linii_noi = linii[:i] + [nou] + linii[i + 1:]
                modificata = nou
            cod_bug = "\n".join(linii_noi)
            if cod_bug in vazute:
                continue
            vazute.add(cod_bug)
            rezultat = ruleaza(cod_bug + "\n" + p["apel"])
            sintaxa = "SyntaxError" in rezultat[2] or "IndentationError" in rezultat[2]
            if rezultat[:2] == referinta[:2] or sintaxa:
                respinse += 1
                continue
            mutanti.append({
                "id": len(mutanti) + 1,
                "program": p["nume"],
                "categorie": categorie,
                "linie_bug": i + 1,
                "linie_originala": linii[i],
                "linie_modificata": modificata,
                "cod_cu_bug": cod_bug,
                "cod_corect": p["cod"],
            })

with open("mutanti.jsonl", "w", encoding="utf-8") as f:
    for m in mutanti:
        f.write(json.dumps(m, ensure_ascii=False) + "\n")

print("Programe corecte:", len(programe))
print("Programe cu bug păstrate:", len(mutanti))
print("Respinse (nu schimbau comportamentul sau nu erau valide):", respinse)
for categorie, n in Counter(m["categorie"] for m in mutanti).items():
    print("  ", categorie, n)

for m in (mutanti[0], mutanti[len(mutanti) // 2]):
    print("\n--- Exemplu ---")
    print("Categorie:", m["categorie"], "| bug la linia", m["linie_bug"])
    print("Linia originală: ", m["linie_originala"].strip())
    print("Linia modificată:", m["linie_modificata"].strip())
    print(m["cod_cu_bug"])
