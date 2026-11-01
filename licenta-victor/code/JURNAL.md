# Jurnal de laborator: tutore socratic pentru cod

Format intrare: dată, de ce, ce am făcut, rezultat (cifre), decizie / ce urmează, unelte AI folosite, commit.

## 2026-09 · Alegerea temei
- De ce: coordonatorul a propus A (tutore personalizat) și B (knowledge tracing federat).
- Decizie: varianta A, adaptată: personalizare pe niveluri (profil, adaptor pe grup, opțional per elev), evaluare principală cu elevi simulați, trial real mic doar ca validare. Aplicația (frontend/backend) o face altcineva; eu fac modelul.
- Hardware: i9-13900K, RTX 4070 12 GB, 32 GB RAM, Windows + WSL2. MacBook M1 32 GB pentru inferență.
- Unelte AI: Claude (chat) pentru analiza opțiunilor și plan.

## 2026-10-11 · Mediul de lucru
- WSL2 Ubuntu, Python 3.12.3, venv în ~/licenta/.venv.
- torch 2.14.1+cu130, CUDA disponibil. unsloth 2026.10.3, transformers 5.17.0, trl 1.13.0, peft 0.21.2, bitsandbytes 0.50.2.
- Test înmulțire matrice 4096x4096: CPU 0,166 s, GPU 0,0167 s (~10x).

## 2026-10-11 · Primul model: Qwen2.5-1.5B-Instruct, 4 biți
- VRAM la inferență: 1,6 GB.
- Aceeași propoziție: 16 tokeni în engleză, 21 în română (o singură propoziție, doar indiciu).
- Observații: nu respectă constrângeri de format, româna are greșeli.
- LoRA r=16 pe 7 module: 18.464.768 parametri antrenabili din 1.562.179.072 (1,18%).

## 2026-10-22 · Antrenare 1, date de jucărie
- Date: 50 train / 10 val, șablonate, 2 tipuri de bug, 3 formulări fixe pe tip.
- Setări: 3 epoci, 21 pași, batch 2 x acumulare 4, lr 2e-4, warmup 2, adamw_8bit, seed 3407.
- Loss train 2,85 -> ~0,31; loss val 1,953 -> 0,503. VRAM 2,28 GB. Durată 19 s. Adaptor 82 MB.
- Pe un bug nou: pune o întrebare, dar generică (nu arată spre condiția greșită).

## 2026-10-22 · Comparație pe 8 bug-uri noi (original vs jucărie)
- Bloc de cod: original 4/8, jucărie 0/8. Semn de întrebare: 3/8 vs 8/8 (metrică înșelătoare: întrebările originalului erau oferte, nu ghidare).
- Citire manuală: modelul antrenat a învățat stilul, nu diagnosticul. Română stricată („Sunt săt!").
- Concluzie: răspunsurile din date nu depindeau de program, deci modelul a învățat un reflex.

## 2026-10-22 · Generator de bug-uri prin mutații
- 10 programe corecte, 7 categorii de mutații, verificare prin rulare (proces separat, timeout 1 s).
- 29 mutanți păstrați, 0 respinși.
- Probleme găsite: categorii dezechilibrate; la linii șterse „linia bug-ului" e ambiguă; împărțirea trebuie făcută după programul de bază.

## 2026-10-22 · Date v2 și antrenare 2
- 51 train / 9 val / 9 test, împărțite după program (val: media; test: maxim, numara_pare, cauta). 3 răspunsuri șablon pe mutant, plângeri neutre.
- 5 epoci, 35 pași. Loss train 2,749 -> 0,26; loss val 2,058 -> 0,281. VRAM 2,22 GB. 20 s.

## 2026-10-31 · Evaluare v2 pe test (prima atingere a testului; exercițiu)
- Menționează elementul greșit: original 2/9, jucărie 5/9, v2 3/9. Bloc de cod: 3/9, 0/9, 0/9.
- Citire manuală v2: 0 reușite clare, 6 parțiale, 3 ratate. Toate cele 9 răspunsuri încep la fel.
- Metrica „conține linia corectă" dă fals pozitiv la return_in_bucla (aceeași linie, alt indent).

## 2026-10-31 · Diagnostic pe datele de antrenare v2
- Menționează elementul greșit: 33/51. comparatie_inversata 1/6, return_in_bucla 5/12.
- 42/51 răspunsuri distincte; 16/51 încep cu „Rulează funcția pe hârtie cu o listă".
- Concluzie: nu a învățat bine nici ce a văzut; problema e în date, nu în antrenare.

## 2026-10-31 · Decizie: date reale, generate local
- MBPP (programe Python cu teste) + mutații + gemma3:12b prin Ollama scrie întrebarea tutorelui + filtru automat.
- Proiectul reorganizat: exercitii/, scripturi/, date/. Claude Code instalat în WSL.
- Unelte AI: gemma3:12b (generare răspunsuri), Claude Code (rulare și modificare scripturi).

## 2026-10-31 · Prima generare
- Mutanți (scripturi/1_mutanti.py): MBPP complet (974 probleme). Filtru: ≤15 linii, cel puțin o buclă for/while, testele originale trec. 359 programe folosite; 314 au dat cel puțin un mutant. Max 3 mutanți/program, păstrat doar dacă testele pică (timeout 2 s).
- Prima rulare: 662 mutanți. Problemă găsită: la actualizare_stearsa, 5/9 ștergeau o variabilă care nu era în condiția while (bucla se termina normal, deci descrierea pentru profesor era falsă). Reparat: linia se șterge doar dacă variabila apare în condiția unui while.
- După reparație: 661 mutanți. return_in_bucla 275, initializare_stearsa 163, valoare_initiala_gresita 109, comparatie_inversata 65, rest_gresit 34, interval_scurtat 11, actualizare_stearsa 4.
- Prompt profesor (scripturi/2_raspunsuri.py, gemma3:12b, temperatura 0,7, max 150 tokeni): codul cu bug + descrierea categoriei + linia schimbată („doar pentru tine, elevul nu știe"). Cerință: o singură întrebare scurtă (≤2 propoziții), fără să spună greșeala, fără soluție, fără cod; poate numi variabila sau linia; română cu diacritice.
- Filtru automat: respinge dacă >350 caractere, fără „?", conține cod, conține linia originală (dă soluția) sau nu conține un cuvânt-cheie al categoriei (ex. „return", „linia N", numele variabilei).
- Exemplu de antrenare: system „Ești un tutore de programare…" + plângere aleasă din 4 + codul cu bug → întrebarea profesorului. Împărțire 80/10/10 după task_id (seed 0).
- 590 păstrate, 71 respinse (37 nu arată spre greșeală, 33 fără întrebare, 1 dă soluția). Împărțire: 480 train / 56 val / 54 test.
- Calculat pe train+val (536): 86% începeau cu „Observă/Observi/Uită-te"; „Observă" 51%. return_in_bucla care spun direct greșeala: 72/205 (35%). Lungime medie 168 caractere. Toate cu diacritice. Fără scurgere între grupe.
- Corectură: în chat apăruseră 78% și 47%, din greșeală (numărate pe train+val, dar împărțite la 590).

## 2026-11-01 · Regenerare cu 6 stiluri de întrebare
- Schimbare în prompt: fiecare mutant primește un stil de întrebare, fiecare cu un început impus: „Dacă" (exemplu concret de intrare), „Cum" (urmărește o variabilă pas cu pas), „La prima/La ultima" (iterație), „Ce crezi" (rolul unei linii), „Dacă ai pune un print", „De ce". Alegerea e fixă pe id (random.Random(id)), deci nu schimbă împărțirea. În plus: „Nu începe cu «Observă», «Observi» sau «Uită-te»"; dacă începe totuși așa, se cere din nou (max 3 încercări).
- Testat întâi pe 18 exemple din val: prima variantă (stiluri fără început impus) dădea 8/8 întrebări care începeau cu „Ce", adică un șablon nou; de aceea s-au impus începuturile.
- Regenerate 582/661 răspunsuri (cele cu „Observ…/Uită-te" + cele respinse); 79 păstrate. Răspunsurile vechi: date/raspunsuri_brute_v1.jsonl. Filtrul neschimbat.
- 513 păstrate, 148 respinse (toate „nu arată spre greșeală": filtrul pe cuvinte-cheie aruncă întrebări indirecte bune).
- Împărțire: 411 train / 53 val / 49 test. actualizare_stearsa 3 exemple, interval_scurtat 10.
- Calculat pe train+val (464): încep cu „Observă/Observi/Uită-te" 3%; cel mai frecvent început „Ce crezi" 16%; return_in_bucla care spun greșeala 39/160 (24%); lungime medie 128. Un singur răspuns fără diacritice. Fără scurgere între grupe.
- Încă trec răspunsuri care dau soluția: 39 la return_in_bucla, 10 la valoare_initiala_gresita.
- Propunere: refacerea filtrului.

## 2026-11-01 · Git, reguli de lucru, primul commit
- De ce: istoric al scripturilor și datelor, ca să pot reveni la orice versiune; reguli clare pentru Claude Code (fără suprascrieri, testul atins o singură dată, jurnal după fiecare pas).
- Ce am făcut: git init; .gitignore cu .venv/, outputs/, unsloth_compiled_cache/, *.safetensors, exercitii/*/tokenizer.json (2 copii ale tokenizer-ului Qwen, 11 MB fiecare, se pot regenera); folder rezultate/ pentru outputul rulărilor; JURNAL.md creat; în CLAUDE.md adăugate secțiunile „Reguli" și „Jurnal și salvare".
- Rezultat: 39 fișiere în commit, ~1,4 MB (fără ignorarea tokenizer-ului ar fi fost ~24 MB). Adaptoarele din exercitii/ (71 MB fiecare) nu sunt în git.
- Abateri de la reguli înainte să existe: Claude Code a suprascris date/mutanti.jsonl, date/raspunsuri_brute.jsonl (copie în raspunsuri_brute_v1.jsonl) și train/val/test.jsonl; a rulat singur pasul 2 (>10 min); a deschis test.jsonl de două ori, doar pentru verificarea suprapunerii task_id (nu pentru decizii).
- Decizie / ce urmează: refacerea filtrului din 2_raspunsuri.py, cu rezultatele în fișiere noi (*_v2.jsonl).
- Unelte AI: Claude Code (git, .gitignore, completarea jurnalului).
- Commit: dc0131b („punct de pornire").

## 2026-11-01 · Mutare pe GitHub (Code-Club-UVT/Project)
- De ce: tot proiectul trebuie să fie în repository-ul clubului, github.com/Code-Club-UVT/Project, în folderul licenta-victor/ (structură creată de Bran Alexandru: code/, docs/, papers/ cu teza și articolul AINA 2027).
- Ce am făcut: clonat Project în ~/Project; scos licenta-victor/code/.empty; importat istoricul local cu `git subtree add --prefix=licenta-victor/code` (commit-ul dc0131b rămâne în istoric); mutat restul fișierelor (inclusiv .venv, adaptoarele din exercitii/) în licenta-victor/code/; ~/licenta e acum o scurtătură (symlink) spre ~/Project/licenta-victor/code, deci comenzile și căile rămân aceleași. Vechiul folder, cu vechiul .git, păstrat ca rezervă în ~/licenta_vechi.
- Verificat: venv funcționează, CUDA disponibil, git vede repository-ul ~/Project.
- Decizie: CLAUDE.md se publică în repository (deși un alt CLAUDE.md fusese șters din rădăcină pe 2026-10-22). Autorul commit-urilor e doar victor05florea, fără coautor Claude.
- Unelte AI: Claude Code (clonare, import, mutare fișiere).
- Commit-uri: 130de1f (scos .empty), eb8781f (import cu istoric).

## 2026-11-01 · Autentificare GitHub prin SSH, prima trimitere
- De ce: push-ul prin HTTPS a eșuat de două ori („Invalid username or token"; GitHub nu acceptă parola contului, iar tokenul nu a fost recunoscut).
- Ce am făcut: cheie SSH ed25519 (~/.ssh/id_ed25519, fără passphrase), adăugată în contul GitHub victor05florea; remote schimbat pe git@github.com:Code-Club-UVT/Project.git; verificat cu ssh -T și git push --dry-run.
- Rezultat: ramura setup trimisă pe GitHub, la commit-ul 8a7513b (4 commit-uri peste main: dc0131b, 130de1f, eb8781f, 8a7513b). main de pe GitHub neatins (baa6dee).
- Decizie: lucrez pe ramura setup; modificările ajung în main prin pull request, aprobat de Bran Alexandru.
- Unelte AI: Claude Code (diagnostic autentificare, generare cheie, configurare remote).
- Commit: hash-ul acestei intrări se notează în intrarea următoare.
