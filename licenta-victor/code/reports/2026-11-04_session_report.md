# Session report, 2026-11-04

Project: Socratic code tutor (UVT bachelor thesis). A small model is fine-tuned with QLoRA to answer a
beginner's buggy Python code with one guiding question, without giving the solution.

## 1. Summary

| Step | What | Main result |
|---|---|---|
| 2 (extras) | gemma3:12b wrote a tutor question for the 215 extra mutants | 215/215 answered |
| 3 | Filter + fixed split → data v3 | 712 kept of 876; train 570 / val 70 / test 72 |
| 4 | First QLoRA training of Qwen2.5-Coder-7B-Instruct, 3 epochs | 6.4 min, 6.75 GB VRAM; lowest val loss 0.470 after epoch 2 |
| 5 | Base model vs. adapter after each epoch, on the 70 val examples | ok answers: base 3, epoch 1 63, epoch 2 58, epoch 3 62 |

**Decision:** the adapter after epoch 1 (`outputs/v3_r16_lr2e-4/checkpoint-72`) is the current best.
**Main finding:** after training, the model asks one short guiding question instead of explaining and fixing
the bug. But about 10–17% of the questions are about the code in general, not about the bug.
**Test set:** not used. Every decision today was made on the validation set.

Commits today: `a53c2f3` (data v3), `293dcae` (training + evaluation), plus the commit of this report.

## 2. Starting point

At the end of 2026-11-01 (last commit `6d2b499`):
- Data v2 had 552 good tutor answers. It was unbalanced: `return_in_loop` was 43% of train,
  `shortened_range` had 6 train examples and `deleted_update` 2. The validation set had no example of either.
- To fix this, step 1b generated 215 extra mutants (buggy programs) of the rare bug types, saved in
  `data/mutants_extra.jsonl`. These did not have tutor questions yet.

## 3. Step 2 on the extra mutants: tutor questions from gemma3:12b

**Why:** each extra mutant needs a "teacher" answer (a guiding question) before it can be used for training.

**Command (run by Victor, ~4 minutes):**
```bash
python scripts/2_responses.py --mutants data/mutants_extra.jsonl --raw data/raw_responses_extra.jsonl \
  2>&1 | tee results/2026-11-04_2106_2_responses_extra.txt
```

**Result:** 215 of 215 answered, written to `data/raw_responses_extra.jsonl`. Two example answers:
- *shortened_range*, `for i in range(0, n - 1):` → "How does the value of `i` change with each iteration of the loop,
  and how does that affect which elements of the string `s` are checked?"
- *shortened_range*, `for k in range(n):` → "If `n` is 5, `x` is 2, and `y` is 1, what value do you expect the
  function to return, and why might the loop's current ending point cause a problem in finding the maximum value?"

## 4. Step 3: filter and split → data v3

**Why:** the teacher's answers are not all good. Some give the fix, some state the bug outright, and some ask
something unrelated to the bug. The filter (`scripts/3_filter.py`, written on 2026-11-01) removes them.
The split into train/val/test is fixed per program, before filtering, so one program never ends up in two
sets. That prevents data leakage (the model being tested on programs it trained on).

**Command (run by Claude Code, a few seconds):**
```bash
python scripts/3_filter.py 2>&1 | tee results/2026-11-04_2111_3_filter.txt
```

**Result:**
- 876 answers (661 base + 215 extra): **712 kept**, 164 rejected
  (96 do not point at the bug, 46 state the bug, 22 give the solution).
- Of the 215 extras: 160 kept, 55 rejected (48 do not point at the bug, 7 give the solution).
- Kept per category: return_in_loop 233, deleted_initialization 137, flipped_comparison 101,
  wrong_initial_value 93, shortened_range 81, wrong_remainder 34, deleted_update 33.
- Split: programs 256 train / 31 val / 31 test; examples **570 train / 70 val / 72 test**.

**Balance before and after (train set):**

| Category | v2 | v3 |
|---|---|---|
| return_in_loop | 189 (43%) | 189 (33%) |
| deleted_initialization | — | 107 (19%) |
| flipped_comparison | — | 76 (13%) |
| wrong_initial_value | — | 73 (13%) |
| shortened_range | 6 | 70 (12%) |
| deleted_update | 2 | 28 (5%) |
| wrong_remainder | — | 27 (5%) |

Val v3: return_in_loop 21, deleted_initialization 15, flipped_comparison 12, wrong_initial_value 10,
shortened_range 6, wrong_remainder 4, deleted_update 2. Every category is now in val, but the last two are
too small for conclusions about a single category.

**Checks:**
- no program in two splits, and no duplicate ids;
- every v2 train/val example is still in v3 train/val, with 131 new in train and 16 new in val;
- Claude Code read the 19 val answers for the extra mutants. 16 were kept and 3 rejected. The rejection of
  #662 is correct (it gives the fix: "end it at `n` instead of `n + 1`"). #779 and #816 were good questions that
  were rejected anyway, because their wording ("all the possible pairs", "every row") is not in the filter's word lists.
  #663 was kept but is borderline. The filter was **not** tuned again, to avoid fitting it to a few val examples.

**Decision:** data v3 is good enough for the first training. Commit `a53c2f3`.

## 5. Step 4: first QLoRA training

**Why:** the first real tutor model, on the full v3 data.

**What QLoRA is, briefly:** the 7-billion-parameter model is loaded in 4-bit (compressed), so it fits in
12 GB. Its weights stay frozen. Training only changes a small add-on called a LoRA adapter (about 40 million
extra numbers), which is saved separately (~240 MB per checkpoint).

**New script `scripts/4_train.py`, with these settings:**
- base model `unsloth/Qwen2.5-Coder-7B-Instruct-bnb-4bit`;
- LoRA rank r = 16, alpha = 16, applied to all attention and MLP layers (q, k, v, o, gate, up, down);
- learning rate 2e-4, linear decay, 5 warm-up steps, optimizer AdamW 8-bit;
- 8 examples per update (batch 4 × gradient accumulation 2), so 72 updates per epoch and 216 in total;
- max length 512 tokens. The longest example is 304 tokens (measured);
- the loss is computed only on the tutor's answer, not on the system prompt or the student's code;
- validation loss every 12 updates; the adapter is saved at the end of every epoch, so the epoch can be chosen
  afterwards on val;
- the script refuses to overwrite an existing output folder.

**Before the run:** a 12-update test on the small Qwen2.5-1.5B model (it worked). gemma3 was unloaded from the GPU
(`ollama stop gemma3:12b`) because it was holding ~8.6 GB.

**Command (run by Victor):**
```bash
python scripts/4_train.py 2>&1 | tee results/2026-11-04_2124_4_train.txt
```

**Result:** 6.4 minutes, peak GPU memory 6.75 GB.

| Point | Train loss (approx.) | Val loss |
|---|---|---|
| step 12 | 1.01 | 0.920 |
| end of epoch 1 (step 72) | 0.63 | 0.527 |
| end of epoch 2 (step 144) | 0.44 | **0.470** (lowest) |
| step 168 | 0.21 | 0.508 |
| end of epoch 3 (step 216) | 0.23 | 0.509 |

**Interpretation:** loss measures how "surprised" the model is by the correct answer, so lower is better.
In epoch 3, train loss halves while val loss goes up. That is **overfitting**: the model starts memorizing the
training answers instead of learning the general pattern. By val loss, epoch 2 is best.

## 6. Step 5: what the models actually write (evaluation on val)

**Why:** a low loss does not guarantee a good tutor. We have to look at the generated answers.

**New script `scripts/5_eval_val.py`:** four models answer all 70 val examples. They are the base model
(no adapter) and the adapters at step 72, 144 and 216 (end of epochs 1, 2 and 3). Decoding is greedy (always
the most likely word, so the results are reproducible), with at most 200 new tokens. Every answer is judged
with the same rules as the data filter (`rejection_reason` from `3_filter.py`). Test set not read.

**Problem found and fixed:** the first version loaded all four models in one process. The second model ran out
of GPU memory, because the first one was not released. Now each model runs in its own process.

**Command (run by Victor, ~6 minutes):**
```bash
python scripts/5_eval_val.py 2>&1 | tee results/2026-11-04_2138_5_eval_val.txt
```
All 280 answers are in `results/2026-11-04_2138_5_eval_val_answers.jsonl`.

**Result (of 70):**

| Verdict | base | epoch 1 (step 72) | epoch 2 (step 144) | epoch 3 (step 216) |
|---|---|---|---|---|
| **ok** | 3 | **63** | 58 | 62 |
| does not point at the bug | 15 | 7 | 12 | 7 |
| states the bug | 1 | 0 | 0 | 0 |
| gives the solution | 1 | 0 | 0 | 1 |
| too long (>350 characters) | 50 | 0 | 0 | 0 |
| mean length (characters) | 563 | 166 | 161 | 153 |

**ok per category:**

| Category (val examples) | base | epoch 1 | epoch 2 | epoch 3 |
|---|---|---|---|---|
| return_in_loop (21) | 1 | 21 | 17 | 20 |
| deleted_initialization (15) | 0 | 15 | 15 | 15 |
| flipped_comparison (12) | 1 | 11 | 8 | 9 |
| wrong_initial_value (10) | 1 | 8 | 10 | 8 |
| shortened_range (6) | 0 | 4 | 4 | 5 |
| wrong_remainder (4) | 0 | 3 | 3 | 4 |
| deleted_update (2) | 0 | 1 | 1 | 1 |

**Extra checks (done by Claude Code):**

| | epoch 1 | epoch 2 | epoch 3 |
|---|---|---|---|
| answers copied word for word from train | 0 | 0 | 2 |
| answers with 2+ questions | 11 | 1 | 7 |
| most common openings | How does 22, On the 20, If you 16, What do 12 | **If you 34**, How does 22, What do 12, On the 2 | How does 22, On the 17, If you 16, What do 12 |

**Examples, same program:**
- Base model (deleted_initialization): "It looks like you're trying to calculate the proportion of zeros ...
  there are a few issues in your code ... 1. **Initialization of `n1`**: You need to initialize `n1` before using it."
  It explains and fixes the bug, which is exactly what the tutor must not do.
- Epoch 1 (return_in_loop, #205): "What do you think the `return` statement on line 6 is doing, and how might it
  affect when the function finishes?"
- Epoch 2, same example: "What do you think happens to the function after it checks the first element of the
  list? Does it continue checking all the elements?" This is a reasonable question, but the filter marks it as not
  pointing at the bug.
- A typical failure (epoch 2, #374, return_in_loop): "If you added a print statement inside the `while` loop ...
  to display the value of `res` ..." This is a generic question about another variable, not about the `return`.

**Interpretation:**
1. Training works: the behavior changes completely, from long explanations that give the fix (3/70 ok) to one
   short guiding question (58–63/70 ok).
2. The lowest val loss (epoch 2) does **not** give the best answers. Epoch 2 often falls back on the same
   "If you added a print statement ..." pattern, and those questions are often about a variable unrelated to the bug.
3. The differences between epochs are small: 63 vs 58 vs 62, so a few examples, judged by keyword rules.
   They are a hint, not a strong result.
4. Epoch 3 memorizes: 2 answers copied from train, and val loss rises.

**Decision:** keep **epoch 1 (step 72)** as the current best adapter. It has the most ok answers, the most varied
questions and no copying. Commit `293dcae`.

## 7. Open problems

- In ~10–17% of answers the tutor asks about the code in general, not about the bug. This is the same "learns the
  style, not the bug" problem seen in the early experiments, though much smaller now.
- The automatic judge only matches keywords: it rejects some good indirect questions and might accept some bad
  ones. A human check of a sample of epoch-1 answers is still needed. So far the manual reading was done by
  Claude Code, not by a person.
- deleted_update (2 val examples) and wrong_remainder (4) are too few to judge per category.
- Small housekeeping: some `scripts/__pycache__/*.pyc` files are tracked in git. They could be added to `.gitignore`.
- `CLAUDE.md` still says the next step is training on `data/train.jsonl`; it should now point to v3 and the
  results above.

## 8. Next steps (to decide)

1. A human reads ~30 epoch-1 val answers and marks them good or bad, to check how far the automatic judge can be trusted.
2. Try a gentler training: learning rate 1e-4 and/or 1–2 epochs, and compare on val with the same script.
3. Maybe make the data more varied (fewer "If you added a print statement" answers).
4. Only at the very end: one evaluation on the test set with the final chosen model.

## 9. Files created today

| File | What it is |
|---|---|
| `data/raw_responses_extra.jsonl` | gemma3 answers for the 215 extra mutants |
| `data/train_v3.jsonl`, `val_v3.jsonl`, `test_v3.jsonl` | data v3 |
| `scripts/4_train.py` | QLoRA training |
| `scripts/5_eval_val.py` | val evaluation of base + checkpoints |
| `results/2026-11-04_2106_2_responses_extra.txt` | log of step 2 |
| `results/2026-11-04_2111_3_filter.txt` | log of step 3 |
| `results/2026-11-04_2124_4_train.txt` | log of training |
| `results/2026-11-04_2138_5_eval_val.txt` | log of the evaluation |
| `results/2026-11-04_2138_5_eval_val_answers.jsonl` | all 280 generated answers with verdicts |
| `outputs/v3_r16_lr2e-4/checkpoint-72/144/216` | the adapters (not in git, too large) |
| `reports/2026-11-04_session_report.md` | this report |

## 10. AI tools used

- **gemma3:12b** (local, Ollama): wrote the teacher questions for the extra mutants. The prompt is in
  `scripts/2_responses.py` / `scripts/tutor_text.py`.
- **Claude Code**: ran the filter, checked the data, wrote `4_train.py` and `5_eval_val.py`, ran the short test runs,
  analyzed the answers, and wrote the journal entries and this report.
  The commands that took more than a few minutes (generation, training, evaluation) were run by Victor.
- Tutor prompt used for training and evaluation: system "You are a programming tutor. Never give the solution.
  Ask questions that guide the student." + one of the student complaints + the buggy code.

## Glossary

- **Mutant:** a correct program with one bug inserted on purpose (7 bug types).
- **Train / val / test:** the model learns from train; settings are chosen on val; test is used once at the end
  for an honest final number.
- **Epoch:** one full pass over the training data.
- **Loss:** a number for how wrong the model's predictions are; lower is better.
- **Overfitting:** the model memorizes the training data and gets worse on new data.
- **Checkpoint:** a saved copy of the adapter at a given moment of training.
- **Greedy decoding:** the model always picks the most likely next word, so the same input gives the same output.
- **Token:** a piece of a word; the model reads and writes tokens.

## 11. Plan for the next session (added at the end of the session)

Commits of this session: `a53c2f3` (data v3), `293dcae` (training + evaluation), `b5c3283` (report),
plus the commit that adds this section.

Recommended order:

1. **Human check of the automatic judge (first, ~30 min, no GPU).** Every decision so far (which answers stay
   in the data, which epoch is best) relies on keyword rules in `3_filter.py`, and the manual readings were done
   by Claude Code. Victor reads ~30 epoch-1 val answers from
   `results/2026-11-04_2138_5_eval_val_answers.jsonl` and marks each one good or bad (good = a question that
   leads toward the bug and does not give the fix). Compare with the automatic verdicts: if they agree on most
   answers, the val numbers can be trusted; if not, the judge has to be improved before further comparisons.
   This is also a result for the thesis (how reliable the automatic evaluation is).
2. **Measure the noise (~15 min GPU).** Train the same settings again with another seed and evaluate. If the ok
   count moves by ±5 just from the seed, then the gap between epoch 1 (63) and epoch 2 (58) is noise and must
   not be over-interpreted.
3. **Gentler training (~15 min GPU per run).** Learning rate 1e-4, 2 epochs, and compare with the current best on
   val with `5_eval_val.py`. Goal: fewer generic questions and no repeated "If you added a print statement".
4. **Data variety, if step 3 does not help.** Limit how many training answers use the same opening style.
5. **At the very end:** one evaluation on the test set with the chosen model, and nothing changed after it.

Housekeeping still open: add `__pycache__/` to `.gitignore`; update the "next step" line in `CLAUDE.md`.
