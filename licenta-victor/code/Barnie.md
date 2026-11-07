# Barnie: summary of the work so far and all plans

Written 2026-11-07. Source: JOURNAL.md, reports/, results/ and the git history (branch `setup`).
Nothing new was run for this file.

## 1. The project in one paragraph

Bachelor thesis (UVT, Computer Science): fine-tune a small model with QLoRA so it acts as a programming tutor for
beginners (CoderDojo). It must ask one short guiding question in English and must not give the solution or state the
bug. Hardware: WSL2 Ubuntu, RTX 4070 (12 GB), teacher model gemma3:12b through Ollama. The student model is
Qwen2.5-Coder-7B-Instruct (4-bit) with a LoRA adapter.

## 2. What was done, in order

| When | Step | Result |
|---|---|---|
| 2026-09 | Topic: option A (personalised tutor), simulated students for evaluation | Application built by someone else; I build the model |
| 2026-10-11 | Environment, first model Qwen2.5-1.5B 4-bit, LoRA r=16 | Works; 18.5M trainable parameters (1.18%) |
| 2026-10-22 | Toy experiments on templated data | The model learns the tutor's style, not how to find the bug |
| 2026-10-31 | Decision: real data. MBPP programs + injected bugs (7 categories) + gemma3:12b writes the tutor question + automatic filter | Pipeline: `scripts/1_mutants.py`, `2_responses.py` |
| 2026-11-01 | Six question styles; git; move to the club's repo (`Code-Club-UVT/Project`, branch `setup`); SSH key; pull request was declined for Romanian text, so the whole project was translated to English; tutor switched to English | 540 answers kept at first |
| 2026-11-01 | Stricter filter (`3_filter.py`), split fixed by program before filtering (no leakage); extra mutants for rare categories (`1b_extra_mutants.py`) | Data v3: 876 answers, 712 kept; train 570 / val 70 / test 72 |
| 2026-11-04 | First QLoRA training (`4_train.py`, 6.4 min, 6.75 GB VRAM) and val evaluation (`5_eval_val.py`) | Base: 3/70 "form ok" (long answers, often give the fix). Trained: 58-63/70 "form ok", one short question |
| 2026-11-04 | Seed and learning-rate experiments (`6_run_experiments.sh`) | Seed noise is about 8 answers, bigger than the gaps between epochs. lr 1e-4 is not better. Recipe: lr 2e-4, 2 epochs |
| 2026-11-05 | Human check of the judge: I marked 30 val answers good/bad | Judge said ok for 25 of 30; I said good for only 8 of 30 (27%). Agreement 11 of 30 |
| 2026-11-06 | Draft rubric of a good tutor question (`reports/question_rubric_draft.md`) | 5 rules for a good question, 6 bad patterns, what worked per bug type |

## 3. Main findings

1. Fine-tuning changes the behaviour completely: from long explanations to one short guiding question.
2. The automatic judge (keyword rules) only checks form (short, one question, mentions the right area). It is far
   too lenient. Its "ok" rate of about 85-90% must not be reported as tutor quality. Real quality by my marks:
   about 27% good (only 30 answers, one marker, one checkpoint, so a rough estimate).
3. Training settings are not the bottleneck. Seed noise (55 vs 63 ok at epoch 1) is larger than the differences
   between epochs. The earlier claim "epoch 1 is best" is not supported.
4. Strong: return_in_loop (5 of 7 good). Weak: deleted_initialization (1 of 8), wrong_initial_value (1 of 6),
   flipped_comparison (1 of 5), and no good example yet for shortened_range, wrong_remainder, deleted_update.
5. Bad patterns: "add a print in the loop" when the bug is not in the loop; naming the variable and the missing
   step (nearly the fix); asking about the correct branch; asking about the wrong line or variable; vague
   questions; two questions in one.
6. The data itself teaches these weaknesses. The filter lets form-ok but unhelpful teacher questions into the
   training set, so the data and the judge are what to improve, not the learning rate.

## 4. Open problems and housekeeping

- The test set has not been used (72 examples, to be used once at the end).
- The "Next step" line in `CLAUDE.md` is out of date (it still says QLoRA training is next).
- `__pycache__/` is not in `.gitignore` (one untracked `.pyc` file).
- The rubric draft has two open questions for me (Q15 borderline; Q3 and Q12 are weak examples).
- Val has only 2 examples of deleted_update and 4 of wrong_remainder, too few for per-category conclusions.
- The journal has no entry yet for the rubric commit `fb4b04f`.
- The pull request into `main` needs approval from Bran Alexandru; `main` on GitHub is still untouched.

## 5. Plans (proposals, in the order I would do them; you decide)

These come from the journal's "Next" lines and the plan in `reports/2026-11-04_session_report.md`, plus my own
suggestions. Items 1-3 are from the journal. Items 4-8 are my suggestions and are not decided.

1. **Finish the rubric.** You edit `reports/question_rubric_draft.md` and answer the two open points. No GPU.
2. **Decide how the final model is evaluated.** Options: (a) a stricter judge built from the rubric (for example
   gemma3:12b grading each answer against the rubric, checked against your 30 marks); (b) human marks on a fixed
   sample of answers; (c) both. Check any new judge on the 30 marked answers first (it should agree with you far
   more than the 11 of 30 of the keyword judge).
3. **Improve the data, not the training.** Re-filter or regenerate the training answers with the rubric so the bad
   patterns (P1-P6) are removed, with extra care for deleted_initialization, wrong_initial_value and
   flipped_comparison. Write to new files (data v4), keep v3. Fix the teacher prompt so it asks about the buggy
   line, not "add a print everywhere". Cap how many answers share an opening style.
4. **Add more examples for the rare categories** with good questions (shortened_range, wrong_remainder,
   deleted_update). This may need new mutants or a larger val set for them.
5. **Retrain** with the standard recipe (lr 2e-4, 2 epochs) on data v4, with at least 2 seeds, and compare averages
   on val using the new judge plus human marks. One run is not enough because of the noise.
6. **Compare against the base model and the current v3 model** on the same val answers, so the thesis can show
   what the data improvement gave.
7. **Test set, once, at the end,** with the chosen model and nothing changed afterwards.
8. **Housekeeping:** add `__pycache__/` to `.gitignore`, update the "Next step" line in `CLAUDE.md`, add the missing
   journal entry, then open the pull request to `main`.

Later, as in the original topic: personalisation by level (profile, adapter per group), evaluation with simulated
students, and only a small real trial as validation.

## 6. Rules I follow (from CLAUDE.md)

Train only on `data/train*.jsonl`; choose settings on val; test once at the end. Do not delete or overwrite files
without asking; write results to new files. Scripts over 10 minutes are run by you. After every step: journal
entry, saved run output, and a commit. Commits are authored by you only.
