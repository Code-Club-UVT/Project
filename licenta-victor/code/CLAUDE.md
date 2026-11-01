# Proiect licență: tutore socratic pentru cod

Licență UVT, Informatică. Fine-tuning QLoRA pe un model mic, ca să fie tutore de programare
pentru începători (CoderDojo): pune întrebări de ghidare în română, nu dă soluția.

## Mediu
- WSL2 Ubuntu pe Windows, RTX 4070 (12 GB VRAM), 32 GB RAM
- Proiect în ~/licenta, mediu virtual .venv (activare: source .venv/bin/activate)
- Instalat: torch, unsloth, peft, trl, bitsandbytes, transformers, datasets
- Ollama în WSL, model profesor: gemma3:12b

## Structură
- exercitii/: experimentele vechi (date de jucărie, Qwen 1.5B), nu se mai modifică
- scripturi/1_mutanti.py: ia programe din MBPP și introduce bug-uri controlate (7 categorii)
- scripturi/2_raspunsuri.py: gemma3:12b scrie întrebarea tutorelui, un filtru aruncă răspunsurile proaste
- date/: mutanti.jsonl, train/val/test.jsonl (împărțite după task_id ca să nu existe scurgere de date)

## Ce am învățat până acum
- Pe date șablonate, modelul învață stilul tutorelui, nu să găsească bug-ul
- Qwen 1.5B scrie română stricată; de verificat pe modele mai mari
- Următorul pas: antrenare QLoRA pe Qwen2.5-Coder-7B-Instruct cu date/train.jsonl

## Cum vreau să lucrezi
- Sunt începător în ML: explică scurt și simplu, fără termeni neexplicați
- Dă-mi comanda, o rulez, apoi explică ce s-a întâmplat
- Nu atinge setul de test pentru decizii; deciziile se iau pe validare

## Reguli
- Nu șterge și nu suprascrie fișiere fără să-mi ceri voie; scrie rezultatele în fișiere noi
- Nu modifica exercitii/ și nu rula pip install fără aprobare
- Antrenezi doar pe date/train.jsonl; setările se aleg pe val.jsonl; test.jsonl se atinge o singură dată, la final
- Scripturile lungi (peste 10 minute): dă-mi comanda și le rulez eu

## Jurnal și salvare (obligatoriu după fiecare pas)
1. Adaugă o intrare la finalul JURNAL.md, în formatul de acolo: de ce, ce ai făcut, cifre exacte, decizie, unelte AI și prompturile folosite.
2. Salvează outputul rulărilor: python scripturi/X.py 2>&1 | tee rezultate/$(date +%F_%H%M)_X.txt
3. Fă git commit cu un mesaj care descrie pasul, și notează hash-ul în jurnal.
Nu șterge și nu rescrie intrări vechi din jurnal; doar adaugi.
