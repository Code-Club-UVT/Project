# Bachelor thesis project: Socratic code tutor

UVT bachelor thesis, Computer Science. QLoRA fine-tuning of a small model to act as a programming
tutor for beginners (CoderDojo): it asks guiding questions in English and does not give the solution.
(Until 2026-11-01 the tutor was Romanian; it is English-only for now.)

## Environment
- WSL2 Ubuntu on Windows, RTX 4070 (12 GB VRAM), 32 GB RAM
- Project in ~/licenta (a symlink to ~/Project/licenta-victor/code, a clone of github.com/Code-Club-UVT/Project), virtual env .venv (activate: source .venv/bin/activate)
- Installed: torch, unsloth, peft, trl, bitsandbytes, transformers, datasets
- Ollama in WSL, teacher model: gemma3:12b

## Structure
- experiments/: the old experiments (toy data, Qwen 1.5B), no longer changed (see experiments/README.md)
- scripts/1_mutants.py: takes programs from MBPP and injects controlled bugs (7 categories)
- scripts/2_responses.py: gemma3:12b writes the tutor question, a filter drops bad answers
- data/: mutants.jsonl, train/val/test.jsonl (split by task_id so there is no data leakage); older generations in data/archive/
- results/: saved output of every run

## Language
- Everything is in English: code, files, docs, the journal, and the tutor's inputs and answers.
- Exception: experiments/ and data/archive/ are old records made with Romanian data; they are kept as they were.
- Talk to me in English.

## What I have learned so far
- On templated data, the model learns the tutor's style, not how to find the bug
- Qwen 1.5B writes broken Romanian; to be checked on larger models
- Next step: QLoRA training on Qwen2.5-Coder-7B-Instruct with data/train.jsonl

## How I want you to work
- I am a beginner in ML: explain briefly and simply, with no unexplained terms
- Give me the command, I run it, then explain what happened
- Do not use the test set for decisions; decisions are made on validation

## Rules
- Do not delete or overwrite files without asking me; write results to new files
- Do not modify experiments/ and do not run pip install without approval
- Train only on data/train.jsonl; settings are chosen on val.jsonl; test.jsonl is used only once, at the end
- Long scripts (over 10 minutes): give me the command and I run them
- Git commits are authored by me only: no Co-Authored-By or other Claude attribution

## Journal and saving (required after every step)
1. Add an entry at the end of JOURNAL.md, in the format used there: why, what you did, exact numbers, decision, AI tools and the prompts used.
2. Save the output of runs: python scripts/X.py 2>&1 | tee results/$(date +%F_%H%M)_X.txt
3. Make a git commit with a message that describes the step, and note the hash in the journal.
Do not delete or rewrite old journal entries; only append.
