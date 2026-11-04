# Lab journal: Socratic code tutor

Entry format: date, why, what I did, result (numbers), decision / next step, AI tools used, commit.

Note: this journal was written in Romanian until 2026-11-01 and then translated in place (the
Romanian original is in git history, file JURNAL.md). File, folder and category names below use
the English names introduced on 2026-11-01; the full mapping is in that day's entry. Commits made
before that day use the Romanian names. Romanian quotes are kept, with a translation in brackets.

## 2026-09 · Choosing the topic
- Why: the supervisor proposed A (personalised tutor) and B (federated knowledge tracing).
- Decision: option A, adapted: personalisation in levels (profile, adapter per group, optionally per student), main evaluation with simulated students, a small real trial only as validation. The application (frontend/backend) is built by someone else; I build the model.
- Hardware: i9-13900K, RTX 4070 12 GB, 32 GB RAM, Windows + WSL2. MacBook M1 32 GB for inference.
- AI tools: Claude (chat) for analysing the options and planning.

## 2026-10-11 · Work environment
- WSL2 Ubuntu, Python 3.12.3, venv in ~/licenta/.venv.
- torch 2.14.1+cu130, CUDA available. unsloth 2026.10.3, transformers 5.17.0, trl 1.13.0, peft 0.21.2, bitsandbytes 0.50.2.
- 4096x4096 matrix multiplication test: CPU 0.166 s, GPU 0.0167 s (~10x).

## 2026-10-11 · First model: Qwen2.5-1.5B-Instruct, 4-bit
- VRAM at inference: 1.6 GB.
- Same sentence: 16 tokens in English, 21 in Romanian (a single sentence, only a hint).
- Observations: does not follow format constraints, the Romanian has mistakes.
- LoRA r=16 on 7 modules: 18,464,768 trainable parameters out of 1,562,179,072 (1.18%).

## 2026-10-22 · Training 1, toy data
- Data: 50 train / 10 val, templated, 2 bug types, 3 fixed phrasings per type.
- Settings: 3 epochs, 21 steps, batch 2 x accumulation 4, lr 2e-4, warmup 2, adamw_8bit, seed 3407.
- Train loss 2.85 -> ~0.31; val loss 1.953 -> 0.503. VRAM 2.28 GB. Duration 19 s. Adapter 82 MB.
- On a new bug: it asks a question, but a generic one (does not point at the wrong condition).

## 2026-10-22 · Comparison on 8 new bugs (base vs toy)
- Code block: base 4/8, toy 0/8. Question mark: 3/8 vs 8/8 (misleading metric: the base model's questions were offers, not guidance).
- Manual reading: the trained model learned the style, not the diagnosis. Broken Romanian („Sunt săt!" [not a real word]).
- Conclusion: the answers in the data did not depend on the program, so the model learned a reflex.

## 2026-10-22 · Mutation-based bug generator
- 10 correct programs, 7 mutation categories, checked by running them (separate process, 1 s timeout).
- 29 mutants kept, 0 rejected.
- Problems found: unbalanced categories; for deleted lines the "bug line" is ambiguous; the split must be done by base program.

## 2026-10-22 · Data v2 and training 2
- 51 train / 9 val / 9 test, split by program (val: media; test: maxim, numara_pare, cauta). 3 templated answers per mutant, neutral complaints.
- 5 epochs, 35 steps. Train loss 2.749 -> 0.26; val loss 2.058 -> 0.281. VRAM 2.22 GB. 20 s.

## 2026-10-31 · Evaluation of v2 on test (first use of the test set; an exercise)
- Mentions the faulty element: base 2/9, toy 5/9, v2 3/9. Code block: 3/9, 0/9, 0/9.
- Manual reading of v2: 0 clear successes, 6 partial, 3 misses. All 9 answers start the same way.
- The "contains the correct line" metric gives a false positive for return_in_loop (same line, different indentation).

## 2026-10-31 · Diagnostic on the v2 training data
- Mentions the faulty element: 33/51. flipped_comparison 1/6, return_in_loop 5/12.
- 42/51 distinct answers; 16/51 start with „Rulează funcția pe hârtie cu o listă" [Run the function on paper with a list].
- Conclusion: it did not learn well even what it saw; the problem is in the data, not in the training.

## 2026-10-31 · Decision: real data, generated locally
- MBPP (Python programs with tests) + mutations + gemma3:12b via Ollama writes the tutor question + automatic filter.
- Project reorganised: experiments/, scripts/, data/. Claude Code installed in WSL.
- AI tools: gemma3:12b (generating answers), Claude Code (running and editing scripts).

## 2026-10-31 · First generation
- Mutants (scripts/1_mutants.py): full MBPP (974 problems). Filter: ≤15 lines, at least one for/while loop, the original tests pass. 359 programs used; 314 gave at least one mutant. Max 3 mutants/program, kept only if the tests fail (2 s timeout).
- First run: 662 mutants. Problem found: for deleted_update, 5/9 deleted a variable that was not in the while condition (the loop ended normally, so the description given to the teacher was false). Fixed: the line is deleted only if the variable appears in the condition of a while.
- After the fix: 661 mutants. return_in_loop 275, deleted_initialization 163, wrong_initial_value 109, flipped_comparison 65, wrong_remainder 34, shortened_range 11, deleted_update 4.
- Teacher prompt (scripts/2_responses.py, gemma3:12b, temperature 0.7, max 150 tokens), written in Romanian at the time: the buggy code + the category description + the changed line („doar pentru tine, elevul nu știe" [only for you, the student does not know]). Requirement: a single short question (≤2 sentences), without saying the mistake, without the solution, without code; may name the variable or the line; Romanian with diacritics.
- Automatic filter: rejects if >350 characters, no "?", contains code, contains the original line (gives the solution) or does not contain a keyword of the category (e.g. "return", "linia N" [line N], the variable name).
- Training example: system „Ești un tutore de programare…" [You are a programming tutor…] + a complaint chosen from 4 + the buggy code → the teacher's question. Split 80/10/10 by task_id (seed 0).
- 590 kept, 71 rejected (37 do not point at the bug, 33 no question, 1 gives the solution). Split: 480 train / 56 val / 54 test.
- Computed on train+val (536): 86% started with „Observă/Observi/Uită-te" [Notice/Look at]; „Observă" 51%. return_in_loop answers that state the mistake directly: 72/205 (35%). Mean length 168 characters. All with diacritics. No leakage between splits.
- Correction: the chat had shown 78% and 47% by mistake (counted on train+val, but divided by 590).

## 2026-11-01 · Regeneration with 6 question styles
- Prompt change: each mutant gets a question style, each with a required opening: „Dacă" [If] (a concrete input example), „Cum" [How] (trace a variable step by step), „La prima/La ultima" [On the first/last] (iteration), „Ce crezi" [What do you think] (the role of a line), „Dacă ai pune un print" [If you added a print], „De ce" [Why]. The choice is fixed per id (random.Random(id)), so it does not change the split. Also: „Nu începe cu «Observă», «Observi» sau «Uită-te»" [Do not start with …]; if it still starts that way, it is asked again (max 3 attempts).
- Tested first on 18 val examples: the first variant (styles without a required opening) gave 8/8 questions starting with „Ce" [What], i.e. a new template; that is why openings were imposed.
- Regenerated 582/661 answers (those starting with „Observ…/Uită-te" + the rejected ones); 79 kept. Old answers: data/archive/gen1_raw_responses.jsonl. Filter unchanged.
- 513 kept, 148 rejected (all "does not point at the bug": the keyword filter throws away good indirect questions).
- Split: 411 train / 53 val / 49 test. deleted_update 3 examples, shortened_range 10.
- Computed on train+val (464): start with „Observă/Observi/Uită-te" 3%; most frequent opening „Ce crezi" 16%; return_in_loop answers that state the mistake 39/160 (24%); mean length 128. Only one answer without diacritics. No leakage between splits.
- Answers that give the solution still get through: 39 for return_in_loop, 10 for wrong_initial_value.
- Proposal: rework the filter.

## 2026-11-01 · Git, working rules, first commit
- Why: a history of scripts and data, so I can go back to any version; clear rules for Claude Code (no overwriting, the test set used only once, journal after every step).
- What I did: git init; .gitignore with .venv/, outputs/, unsloth_compiled_cache/, *.safetensors, experiments/*/tokenizer.json (2 copies of the Qwen tokenizer, 11 MB each, can be regenerated); results/ folder for run outputs; journal created; "Rules" and "Journal and saving" sections added to CLAUDE.md.
- Result: 39 files in the commit, ~1.4 MB (without ignoring the tokenizer it would have been ~24 MB). The adapters in experiments/ (71 MB each) are not in git.
- Rule breaks from before the rules existed: Claude Code overwrote the mutants file, the raw answers file (copy kept, now data/archive/gen1_raw_responses.jsonl) and train/val/test.jsonl; ran step 2 itself (>10 min); opened test.jsonl twice, only to check task_id overlap (not for decisions).
- Decision / next step: rework the filter in 2_responses.py, with the results in new files.
- AI tools: Claude Code (git, .gitignore, completing the journal).
- Commit: dc0131b ("punct de pornire" [starting point]).

## 2026-11-01 · Move to GitHub (Code-Club-UVT/Project)
- Why: the whole project must be in the club's repository, github.com/Code-Club-UVT/Project, in the licenta-victor/ folder (structure created by Bran Alexandru: code/, docs/, papers/ with the thesis and the AINA 2027 paper).
- What I did: cloned Project into ~/Project; removed licenta-victor/code/.empty; imported the local history with `git subtree add --prefix=licenta-victor/code` (commit dc0131b stays in the history); moved the remaining files (including .venv and the adapters in experiments/) into licenta-victor/code/; ~/licenta is now a shortcut (symlink) to ~/Project/licenta-victor/code, so commands and paths stay the same. The old folder, with the old .git, kept as a backup in ~/licenta_vechi.
- Checked: the venv works, CUDA available, git sees the ~/Project repository.
- Decision: CLAUDE.md is published in the repository (although another CLAUDE.md had been removed from the root on 2026-10-22). Commits are authored only by victor05florea, without a Claude co-author.
- AI tools: Claude Code (cloning, import, moving files).
- Commits: 130de1f (removed .empty), eb8781f (import with history).

## 2026-11-01 · GitHub authentication via SSH, first push
- Why: pushing over HTTPS failed twice ("Invalid username or token"; GitHub does not accept the account password, and the token was not recognised).
- What I did: ed25519 SSH key (~/.ssh/id_ed25519, no passphrase), added to the GitHub account victor05florea; remote changed to git@github.com:Code-Club-UVT/Project.git; checked with ssh -T and git push --dry-run.
- Result: branch setup pushed to GitHub at commit 8a7513b (4 commits on top of main: dc0131b, 130de1f, eb8781f, 8a7513b). main on GitHub untouched (baa6dee).
- Decision: I work on the setup branch; changes reach main through a pull request approved by Bran Alexandru.
- AI tools: Claude Code (diagnosing authentication, generating the key, configuring the remote).
- Commit: 427e7f1.

## 2026-11-01 · Translation of the project to English
- Why: the pull request was declined because a large part of the files and code was in Romanian; the repository must be in English.
- What I did:
  - Renamed (with git mv, so file history is kept): scripturi/ → scripts/, date/ → data/, exercitii/ → experiments/, rezultate/ → results/, JURNAL.md → JOURNAL.md, 1_mutanti.py → 1_mutants.py, 2_raspunsuri.py → 2_responses.py. In experiments/: adaptor_jucarie/ → adapter_toy/, adaptor_v2/ → adapter_v2/, compara.py → compare.py, comparatie*.txt → comparison*.txt, construieste_date.py → build_data.py, diagnostic_antrenare.* → training_diagnostic.*, evalueaza_v2.py → evaluate_v2.py, genereaza_bug.py → generate_bugs.py, mutanti.jsonl → mutants.jsonl.
  - Categories: comparatie_inversata → flipped_comparison, interval_scurtat → shortened_range, valoare_initiala_gresita → wrong_initial_value, initializare_stearsa → deleted_initialization, rest_gresit → wrong_remainder, return_in_bucla → return_in_loop, actualizare_stearsa → deleted_update.
  - JSON keys: categorie → category, linie_bug → bug_line, linie_originala → original_line, linie_modificata → modified_line, cod_cu_bug → buggy_code, cod_corect → correct_code, plangere → complaint, raspuns → response. Marker „(linia a fost ștearsă)" → "(line deleted)". Converted in all .jsonl files; the texts inside (code, tutor questions) unchanged.
  - Code: identifiers, comments and messages translated in scripts/ and experiments/; experiments/README.md added; result .txt files: only header lines and category names translated (16 + 27 + 153 lines), model answers left as generated.
  - Old data moved to data/archive/: gen1_raw_responses.jsonl (first generation), gen2_raw_responses.jsonl, gen2_mutants.jsonl, gen2_train/val/test.jsonl (regeneration with 6 styles).
- What stays in Romanian, on purpose: everything the tutor model reads or writes. The tutor's answers (Romanian beginners are the users), the simulated student complaints in 2_responses.py, the filter keywords that match Romanian answers, and in experiments/ all model inputs verbatim (the recorded results came from them).
- Change in the current pipeline: gemma3's instructions in 2_responses.py are now in English and ask explicitly for a Romanian answer; the tutor system prompt is now „You are a programming tutor. Never give the solution. Ask questions that guide the student. Always answer in Romanian." Because of this, the data must be regenerated (step 2, ~1 h); the output goes to new files data/raw_responses.jsonl and data/train/val/test.jsonl.
- Checks:
  - scripts/1_mutants.py re-run: data/mutants.jsonl byte-for-byte identical to the converted old mutants (661; same counts per category). Output: results/2026-11-01_*_1_mutants.txt.
  - Filter in 2_responses.py on the old answers (gen2): 513 kept, 148 rejected, same as before the translation.
  - English instructions tried on 6 val examples: answers still in Romanian with diacritics, 4/6 pass the filter.
  - experiments/: generate_bugs.py, build_data.py and make_toy_data.py re-run in a scratch folder reproduce their .jsonl files byte for byte; the other scripts need the GPU and adapters and were only syntax-checked.
- AI tools: Claude Code (translation, renaming, conversion and checks). Prompts: the new gemma3 instructions are in scripts/2_responses.py (teacher_request, STYLES).
- Commit: noted in the next entry.

## 2026-11-01 · Tutor switched to English
- Why: decision to make every input, answer and file English and leave Romanian aside for now (simpler scope; the repository must be English).
- Commit of the previous entry (translation of the project): 7084743.
- What I changed in scripts/2_responses.py:
  - Tutor system prompt: "You are a programming tutor. Never give the solution. Ask questions that guide the student." (no "answer in Romanian").
  - Student complaints, now English: "My function doesn't work and I don't understand why." / "Something is wrong in my code, can you help me?" / "The result is not what I expected." / "I don't know what I did wrong here."
  - Teacher prompt to gemma3:12b: same structure as before, ending with "Write in clear, simple English for a beginner. Reply with the question only."
  - Question styles (one per mutant, fixed by id), required openings: "If" (concrete input example), "How" (trace a variable), "On the first"/"On the last" (loop pass), "What do you think" (role of a line), "If you added a print", "Why". Forbidden openings: "Notice", "Look at", "Take a look" (the English equivalents of the Romanian „Observă/Uită-te" that dominated the first generation); up to 3 retries.
  - Filter keywords in English: "line N", "last", "condition", "compar", "remainder", "even", "odd" (plus "return", "range", "%", variable names, as before).
- Check on 12 val examples: all answers in English, varied openings; 9/12 pass the filter, 3 rejected as "does not point at the bug" (good indirect questions); 1 passing answer gives the solution ("Why did you initialize n1 to 1 instead of 0?"). Same weaknesses as the Romanian filter; the filter rework is still pending.
- Not changed: experiments/ and data/archive/ (old Romanian records); scripts/1_mutants.py and data/mutants.jsonl (already language-neutral).
- Next: run step 2 (~1 h) to generate data/raw_responses.jsonl and data/train/val/test.jsonl, then the filter rework.
- AI tools: Claude Code (editing the script, testing the prompt on val, docs).
- Commit: noted in the next entry.

## 2026-11-01 · English generation (step 2)
- Why: first generation of the tutor data in English (see previous entry for the prompt).
- Commit of the previous entry: 110918b.
- Run: python scripts/2_responses.py, output in results/2026-11-01_1304_2_responses.txt (results/2026-11-01_1303_2_responses.txt is empty: a start that was stopped immediately; data checked: 661 rows, 661 unique ids, no duplicates).
- Result: 661 generated, 540 kept, 121 rejected (all "does not point at the bug"). Kept per category: return_in_loop 212, deleted_initialization 155, wrong_initial_value 100, flipped_comparison 38, wrong_remainder 24, shortened_range 9, deleted_update 2. Split: 439 train / 59 val / 42 test.
- Computed on train+val (498): no overlap between splits; openings "If you" 32% (two styles start with "If"), "What do" 18%, "On the" 16%, "How does" 15%, "Why did" 9%, "Why does" 9%; 0 start with Notice/Look at/Take a look; return_in_loop answers that say return is inside the loop 22/193 (11%, Romanian gen2: 24%); wrong_initial_value answers that suggest the correct value 9/93; mean length 136 characters; 33 answers contain two questions.
- Decision / next: rework the filter (it still rejects good indirect questions and lets ~31 solution-giving answers through), results to new files; then decide on category balance (deleted_update 2, shortened_range 9).
- AI tools: gemma3:12b (answers), Claude Code (run and checks).
- Commit: noted in the next entry.

## 2026-11-01 · Stricter filter and fixed split (step 3)
- Why: the step 2 filter (keyword match) let through answers that state the bug or give the fix, and rejected good indirect questions. Also, its split was done over the programs that survive the filter, so any filter change moved programs between train/val/test.
- Commit of the previous entry: f902e3f.
- What I did: new script scripts/3_filter.py (reads data/mutants.jsonl + data/raw_responses.jsonl, writes data/train_v2.jsonl, val_v2.jsonl, test_v2.jsonl; old files untouched). Shared tutor text moved to scripts/tutor_text.py (2_responses.py imports it, behaviour unchanged).
  - Split fixed before filtering: all 314 programs with mutants, shuffled with seed 0, 10% test / 10% val / 80% train → 31 / 31 / 252 programs.
  - New rejections: any answer with "instead of" (gives the alternative = the fix); for shortened_range, the original range(len(x)); "states the bug" for return_in_loop ("put/placing/appear ... inside the loop", "only the first") and for deleted_initialization/deleted_update ("without a starting value", "before it's assigned", "never initialized").
  - New accepted anchors: return_in_loop "stop", "first pass/iteration", "immediately after", "how many times"; flipped_comparison "greater/less/smaller/larger/bigger", "increasing/decreasing"; shortened_range "final", "every/each element", "all the elements"; deleted_update "stop", "never end", "infinite"; for any category, predict-vs-actual questions ("what do you expect ... does the current code produce ...").
  - Complaint per example now chosen with random.Random(id), so it is reproducible.
- How the rules were chosen: Claude Code read all 66 val answers and marked each one (11 kept answers state the bug or give the fix; 8 rejected answers are good questions). With the new rules, all 19 changed decisions on val match the manual marks (val kept 57 → 54 of 66). Generalisation check on train (not used for tuning): 101 decisions change (57 recovered, 34 states the bug, 10 gives the solution); a random sample of 14 was read and all 14 were correct. Test was not looked at.
- Known miss on val: #648 (wrong_remainder) "Why did you choose to check if a number is odd ... when the function is intended to extract even numbers?" gives the bug but passes.
- Result (output: results/2026-11-01_1342_3_filter.txt): 661 answers, 552 kept, 109 rejected (48 does not point at the bug, 46 states the bug, 15 gives the solution). Kept per category: return_in_loop 233, deleted_initialization 137, wrong_initial_value 93, flipped_comparison 53, wrong_remainder 27, shortened_range 7, deleted_update 2. Examples: train 439 / val 54 / test 59. No program overlap between splits.
- Problem left: imbalance. In train return_in_loop is 189/439 (43%); shortened_range 6 and deleted_update 2; val has no example of these two categories.
- Decision / next: decide category balance, then the first QLoRA training on Qwen2.5-Coder-7B-Instruct with data/train_v2.jsonl, settings chosen on data/val_v2.jsonl.
- AI tools: Claude Code (writing the filter, reading and marking the 66 val answers, checks). The manual marks were made by Claude Code, not by me; worth a human second look before training.
- Commit: noted in the next entry.

## 2026-11-01 · Extra mutants for the rare categories (step 1b)
- Why: in data v2, return_in_loop was 43% of train, shortened_range had 6 train examples and deleted_update 2, and val had none of either, so these categories could neither be learned nor measured. Chosen option: generate more rare bugs (instead of capping return_in_loop or leaving it).
- Commit of the previous entry: f66c361.
- What I did:
  - scripts/mutations.py: the mutation rules and helpers moved out of 1_mutants.py (shared). Check: 1_mutants.py re-run in a scratch folder gives data/mutants.jsonl byte-for-byte identical.
  - Wider rules for the rare categories, same meaning: shortened_range also range(n) → range(n - 1), range(a, len(x)) → range(a, len(x) - 1), range(a, n) → range(a, n - 1), range(n + 1) → range(n) (for headers only, no step argument); deleted_update also deletes x += k, x //= k, x >>= k, x = x - k, x = x + 1 etc., only if x is in a while condition above; flipped_comparison also >= ↔ <=; wrong_remainder also % 2 != 0 → % 2 == 0 and % 2 == 1 → % 2 == 0. Each rule unit-checked on sample lines.
  - scripts/1b_extra_mutants.py: for each of the 359 programs, all rare-category candidates (old + wide rules) whose tests fail and whose code is not already in data/mutants.jsonl; rarest categories first; max 3 extra per program; ids continue from 662. Writes data/mutants_extra.jsonl (data/mutants.jsonl unchanged).
  - scripts/2_responses.py: now generation only, with --mutants / --raw options (default files unchanged). Its old filter and split were removed (3_filter.py does both; old version in git history).
  - scripts/3_filter.py: also reads the extra files; base programs keep their v2 split, programs only in the extra file get their own fixed split (seed 1); output now *_v3.jsonl; the shortened_range "gives the solution" check now covers any original range(...). Check: on the base files alone it reproduces train/val/test_v2 exactly.
- Result (output: results/2026-11-01_1406_1b_extra_mutants.txt): available failing rare mutants: shortened_range 112, flipped_comparison 69, deleted_update 35, wrong_remainder 8. Kept 215 extra mutants from 150 programs (4 programs had no earlier mutant): shortened_range 111, flipped_comparison 61, deleted_update 35, wrong_remainder 8. Split of the extras: train 181 / val 19 / test 15. Val extras: shortened_range 9, deleted_update 2, flipped_comparison 8.
- Checks: read 4 samples per category (all match the category description); no duplicate buggy code with data/mutants.jsonl. gemma3 + filter tried on 6 val extras: 5 kept, 1 correctly rejected as giving the fix ("...end it at n instead of n + 1?").
- wrong_remainder stays small (+8): MBPP has few remainder tests in short loop programs.
- Next: step 2 on the extras (~20 min), then 3_filter.py → data v3.
- AI tools: Claude Code (rules, scripts, checks).
- Commit: noted in the next entry.

## 2026-11-04 · Answers for the extra mutants and data v3 (steps 2 + 3)
- Why: the 215 extra mutants from step 1b had no tutor question yet; with them, the rare categories can be learned and measured on val.
- Commit of the previous entry: 6d2b499.
- Run step 2 (by me, ~20 min): python scripts/2_responses.py --mutants data/mutants_extra.jsonl --raw data/raw_responses_extra.jsonl, output in results/2026-11-04_2106_2_responses_extra.txt. 215/215 answered (215 lines in data/raw_responses_extra.jsonl).
- Run step 3: python scripts/3_filter.py, output in results/2026-11-04_2111_3_filter.txt (writes data/train_v3.jsonl, val_v3.jsonl, test_v3.jsonl; v2 files untouched).
- Result: 876 answers (661 base + 215 extra), 712 kept, 164 rejected (96 does not point at the bug, 46 states the bug, 22 gives the solution). The base part gives the same 552 as v2, so the extras added 160 kept / 55 rejected (48 does not point, 7 gives the solution). Kept per category: return_in_loop 233, deleted_initialization 137, flipped_comparison 101, wrong_initial_value 93, shortened_range 81, wrong_remainder 34, deleted_update 33.
- Split: programs train 256 / val 31 / test 31; examples train 570 / val 70 / test 72. Checks: no program overlap between splits, no duplicate ids, every v2 train/val example is still in v3 train/val (131 new in train, 16 new in val).
- Balance (train): return_in_loop 189 (33%, was 43%), deleted_initialization 107 (19%), flipped_comparison 76 (13%), wrong_initial_value 73 (13%), shortened_range 70 (12%, was 6 examples), deleted_update 28 (5%, was 2), wrong_remainder 27 (5%). Val: return_in_loop 21, deleted_initialization 15, flipped_comparison 12, wrong_initial_value 10, shortened_range 6, wrong_remainder 4, deleted_update 2: every category now present in val.
- Check on val extras (19 answers, read by Claude Code): 16 kept, 3 rejected. Rejection of #662 is correct (gives the fix: "end it at n instead of n + 1"). #779 and #816 are good indirect questions that were rejected ("all the possible pairs", "every row" are not anchors): a small loss, not changed. #663 is kept but borderline ("Consider what happens to the last element of the array"). Filter not tuned further, to avoid fitting it to a few val examples.
- Decision: data v3 is good enough for the first QLoRA training (Qwen2.5-Coder-7B-Instruct, train on data/train_v3.jsonl, settings on data/val_v3.jsonl). deleted_update and wrong_remainder stay small (5% each); val counts for them (2 and 4) are too few for per-category conclusions.
- AI tools: gemma3:12b (answers), Claude Code (running the filter, checks, reading val extras, journal).
- Commit: noted in the next entry.

## 2026-11-04 · First QLoRA training on data v3 and evaluation on val (steps 4 + 5)
- Why: first real tutor model: Qwen2.5-Coder-7B-Instruct with a LoRA adapter trained on data/train_v3.jsonl; choose the epoch on data/val_v3.jsonl.
- Commit of the previous entry: a53c2f3.
- New script scripts/4_train.py: model unsloth/Qwen2.5-Coder-7B-Instruct-bnb-4bit (4-bit), LoRA r=16, alpha=16, all attention and MLP projections, learning rate 2e-4 (linear, 5 warm-up steps), batch 4 x gradient accumulation 2 = 8 examples per update, 3 epochs (72 updates per epoch, 216 in total), max length 512 tokens (longest example 304), loss only on the assistant answer (train_on_responses_only), seed 3407. Val loss every 12 updates; adapter saved after each epoch (outputs/v3_r16_lr2e-4/checkpoint-72/144/216, not in git). Refuses to overwrite an existing output folder. Tested first for 12 updates on the small Qwen2.5-1.5B model (works).
- Before training: gemma3:12b unloaded from the GPU (ollama stop gemma3:12b), it was using ~8.6 GB.
- Run (by me): python scripts/4_train.py, output in results/2026-11-04_2124_4_train.txt. Time 6.4 min, peak VRAM 6.75 GB.
- Val loss: 0.920 (step 12), 0.527 (end of epoch 1, step 72), 0.470 (end of epoch 2, step 144, lowest), 0.484 / 0.508 / 0.520 (steps 156-180), 0.509 (end of epoch 3, step 216). Train loss (logged, every 3rd value): ~0.63 at the end of epoch 1, ~0.44 at the end of epoch 2, 0.19-0.25 in epoch 3. Epoch 3: train loss halves while val loss rises = overfitting (memorising train).
- New script scripts/5_eval_val.py: each model (base without adapter, step72, step144, step216) answers all 70 val examples (greedy decoding, max 200 new tokens, batches of 8, each model in its own process so its GPU memory is freed), and each answer is judged with rejection_reason from scripts/3_filter.py (same rules as the data). Test set not read. First version loaded all models in one process and ran out of GPU memory on the second model; fixed.
- Run (by me): python scripts/5_eval_val.py, output in results/2026-11-04_2138_5_eval_val.txt, all 280 answers in results/2026-11-04_2138_5_eval_val_answers.jsonl.
- Result (ok = passes the filter rules), base / step72 / step144 / step216:
  - ok: 3 / 63 / 58 / 62 (of 70)
  - does not point at the bug: 15 / 7 / 12 / 7; states the bug: 1 / 0 / 0 / 0; gives the solution: 1 / 0 / 0 / 1; too long (>350 chars): 50 / 0 / 0 / 0
  - mean length: 563 / 166 / 161 / 153 characters
  - ok per category, step72 / step144 / step216: return_in_loop (21) 21/17/20, deleted_initialization (15) 15/15/15, flipped_comparison (12) 11/8/9, wrong_initial_value (10) 8/10/8, shortened_range (6) 4/4/5, wrong_remainder (4) 3/3/4, deleted_update (2) 1/1/1.
- Extra checks (Claude Code): answers copied word for word from train: 0 / 0 / 2 (step216, another sign of memorising). Openings: step72 "How does" 22, "On the" 20, "If you" 16, "What do" 12; step144 "If you" 34 (mostly "If you added a print statement ..."), "On the" only 2: less varied, and these print questions are often about a variable unrelated to the bug (e.g. #460, #374). Answers with 2+ questions: 11 / 1 / 7. Read all val cases where step144 failed or the checkpoints disagree (17): the verdicts are mostly right; a few step72 "does not point" answers are reasonable (#355, #799).
- Interpretation: training changes the behaviour completely (base: long explanations that often give the fix; adapter: one short guiding question). Lowest val loss (step144) does not give the best answers. The differences between checkpoints are small (63 vs 58 vs 62 of 70, a few examples, judged by keyword rules) so they are not a strong result.
- Decision: keep step72 (end of epoch 1) as the current best adapter: most ok answers, most varied questions, no copying from train. Main weakness for all: ~10-17% of answers ask about the code in general instead of the bug (the "learns style, not the bug" problem seen before).
- Next ideas (to decide): 1-2 epochs with a lower learning rate (1e-4), or more varied data; a human check of a sample of the step72 answers; test set still unused.
- AI tools: Claude Code (writing 4_train.py and 5_eval_val.py, test runs, analysis of the answers, journal). Prompts: tutor system prompt from scripts/tutor_text.py ("You are a programming tutor. Never give the solution. Ask questions that guide the student.") + the student complaint and code, same as in training.
- Commit: noted in the next entry.

## 2026-11-04 · Session report
- Why: a detailed written record of today's work (data v3, first training, val evaluation).
- Commit of the previous entry: 293dcae.
- What I did: wrote reports/2026-11-04_session_report.md (steps, commands, all numbers, decisions, open problems, next steps, glossary). No new runs.
- AI tools: Claude Code (writing the report from the journal, logs and results files).
- Commit: noted in the next entry.

## 2026-11-04 · Plan for the next session
- Why: record what to do next, so the next session starts from a clear plan.
- Commit of the previous entry: b5c3283.
- What I did: added section 11 "Plan for the next session" to reports/2026-11-04_session_report.md. Order: (1) human check of ~30 epoch-1 val answers against the automatic verdicts; (2) same training with another seed, to measure the noise; (3) learning rate 1e-4, 2 epochs; (4) data variety if needed; (5) test set once at the end.
- AI tools: Claude Code (plan and writing).
- Commit: noted in the next entry.

## 2026-11-04 · Unattended experiments prepared (step 6)
- Why: steps 2 and 3 of the plan (noise check with another seed; gentler training) can run without supervision.
- Commit of the previous entry: 0ac7d26.
- What I did: scripts/4_train.py got --seed (default 3407 = the first run, so nothing changes for it); scripts/5_eval_val.py got --skip-base (the base model was already measured, greedy decoding gives the same answers). New scripts/6_run_experiments.sh runs: A) lr 2e-4, 3 epochs, seed 1 → outputs/v3_r16_lr2e-4_seed1, eval of checkpoints 72/144/216; B) lr 1e-4, 2 epochs, seed 3407 → outputs/v3_r16_lr1e-4_ep2, eval of checkpoints 72/144. Each run writes its own log in results/. Both new options tested briefly (1.5B model, 2 updates; eval on 4 val examples).
- To run (by me, ~30 min): bash scripts/6_run_experiments.sh. Results go in the next entry.
- AI tools: Claude Code (script changes, tests).
- Commit: noted in the next entry.
