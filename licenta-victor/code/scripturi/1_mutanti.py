import json
import os
import random
import re
import subprocess
import sys
from collections import Counter
from datasets import load_dataset

random.seed(0)
STERGE = "STERGE"
MAX_PE_PROGRAM = 3

def ind(l):
    return len(l) - len(l.lstrip(" "))

def comparatie_inversata(linii, i):
    l = linii[i]
    if re.match(r"\s*(if|elif|while) ", l):
        if " > " in l:
            return l.replace(" > ", " < ", 1)
        if " < " in l:
            return l.replace(" < ", " > ", 1)
    return None

def interval_scurtat(linii, i):
    m = re.search(r"range\(len\((\w+)\)\)", linii[i])
    if m:
        return linii[i].replace(m.group(0), "range(len(" + m.group(1) + ") - 1)", 1)
    return None

def valoare_initiala_gresita(linii, i):
    m = re.fullmatch(r"(\s+\w+ = )(0|1)", linii[i])
    if m:
        return m.group(1) + ("1" if m.group(2) == "0" else "0")
    return None

def initializare_stearsa(linii, i):
    if re.fullmatch(r"\s+\w+ = (0|1|\[\]|''|\"\"|\w+\[0\])", linii[i]):
        return STERGE
    return None

def rest_gresit(linii, i):
    l = linii[i]
    if "%" in l and "== 0" in l:
        return l.replace("== 0", "== 1", 1)
    return None

def return_in_bucla(linii, i):
    l = linii[i]
    if not l.strip().startswith("return") or i == 0:
        return None
    k = ind(l)
    if ind(linii[i - 1]) <= k:
        return None
    for j in range(i - 1, -1, -1):
        if ind(linii[j]) <= k:
            if ind(linii[j]) == k and re.match(r"\s*(for|while) ", linii[j]):
                return " " * ind(linii[j + 1]) + l.strip()
            return None
    return None

def actualizare_stearsa(linii, i):
    m = re.fullmatch(r"\s+(\w+) [-+]= 1", linii[i])
    if not m:
        return None
    var = m.group(1)
    for x in linii[:i]:
        c = re.match(r"\s*while (.*):", x)
        if c and re.search(rf"\b{var}\b", c.group(1)):
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

def ruleaza(script):
    try:
        r = subprocess.run([sys.executable, "-c", script],
                           capture_output=True, text=True, timeout=2)
        return r.returncode
    except subprocess.TimeoutExpired:
        return "timeout"

def compileaza(cod):
    try:
        compile(cod, "<cod>", "exec")
        return True
    except SyntaxError:
        return False

os.makedirs("date", exist_ok=True)
mbpp = load_dataset("google-research-datasets/mbpp", "full")
programe = [ex for split in mbpp for ex in mbpp[split]]

mutanti = []
folosite = 0
for p in programe:
    cod = p["code"].replace("\r\n", "\n").replace("\t", "    ")
    linii = [l.rstrip() for l in cod.split("\n") if l.strip()]
    if len(linii) > 15 or not any(re.match(r"\s*(for|while) ", l) for l in linii):
        continue
    cod = "\n".join(linii)
    teste = "\n".join(p["test_list"])
    setup = p["test_setup_code"] or ""
    if ruleaza(setup + "\n" + cod + "\n" + teste) != 0:
        continue
    folosite += 1
    candidati = []
    for i in range(len(linii)):
        for categorie, f in MUTATII:
            nou = f(linii, i)
            if nou is None:
                continue
            if nou == STERGE:
                linii_noi = linii[:i] + linii[i + 1:]
                modificata = "(linia a fost ștearsă)"
            else:
                linii_noi = linii[:i] + [nou] + linii[i + 1:]
                modificata = nou
            candidati.append((categorie, i, modificata, "\n".join(linii_noi)))
    random.shuffle(candidati)
    pastrati = 0
    vazute = set()
    for categorie, i, modificata, cod_bug in candidati:
        if pastrati == MAX_PE_PROGRAM:
            break
        if cod_bug in vazute or not compileaza(cod_bug):
            continue
        vazute.add(cod_bug)
        if ruleaza(setup + "\n" + cod_bug + "\n" + teste) == 0:
            continue
        mutanti.append({
            "id": len(mutanti) + 1,
            "task_id": p["task_id"],
            "categorie": categorie,
            "linie_bug": i + 1,
            "linie_originala": linii[i],
            "linie_modificata": modificata,
            "cod_cu_bug": cod_bug,
            "cod_corect": cod,
        })
        pastrati += 1

with open("date/mutanti.jsonl", "w", encoding="utf-8") as f:
    for m in mutanti:
        f.write(json.dumps(m, ensure_ascii=False) + "\n")

print("Programe MBPP folosite:", folosite)
print("Programe cu bug:", len(mutanti))
for c, n in Counter(m["categorie"] for m in mutanti).most_common():
    print("  ", c, n)
