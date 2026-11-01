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
