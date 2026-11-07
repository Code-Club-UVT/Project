# experiments/

Early experiments (October 2026) with Qwen2.5-1.5B-Instruct and toy data. They are kept as a
record and are no longer changed; the current pipeline is in `../scripts/`.

The code, names and messages are in English. The **inputs given to the models are kept in
Romanian, verbatim** (system prompt, student complaints, tutor answer templates, the small test
programs): the tutor targets Romanian beginners, and the recorded results below were produced
from exactly these inputs. Model answers in the `.txt` files are also left as generated.

| File | What it does | Output |
|---|---|---|
| `test_gpu.py` | CPU vs GPU matrix multiplication | printed |
| `test_model.py` | load the base model in 4 bits, VRAM, English vs Romanian token count | printed |
| `test_lora.py` | attach LoRA r=16, count trainable parameters | printed |
| `make_toy_data.py` | 60 templated examples, 2 bug types | `train.jsonl`, `val.jsonl` |
| `train_toy.py` | training 1 on the toy data | `adapter_toy/` |
| `compare.py` | base vs toy adapter on 8 hand-written bugs | `comparison.txt` |
| `generate_bugs.py` | mutation-based bug generator on 10 programs | `mutants.jsonl` |
| `build_data.py` | data v2: 3 templated answers per mutant, split by program | `train_v2.jsonl`, `val_v2.jsonl`, `test_v2.jsonl` |
| `train_v2.py` | training 2 on data v2 | `adapter_v2/` |
| `evaluate_v2.py` | base / toy / v2 on the 9 test mutants | `comparison_v2.txt` |
| `training_diagnostic.py` | adapter v2 on its own training examples | `training_diagnostic.txt` |

Adapter weights (`*.safetensors`) and tokenizer copies are not in git.

Translation check (2026-11-01): after translating, `generate_bugs.py`, `build_data.py` and
`make_toy_data.py` were re-run and reproduce their `.jsonl` files byte for byte.
