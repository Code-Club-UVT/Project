#!/bin/bash
# Step 6: two training experiments + val evaluation, run one after the other without supervision.
#   A) same settings as the first run (lr 2e-4, 3 epochs), another seed -> how much the results vary by chance
#   B) gentler training: lr 1e-4, 2 epochs
# Every run writes its own log in results/; nothing existing is overwritten (4_train.py refuses existing folders).
# Run from ~/licenta with the .venv active:  bash scripts/6_run_experiments.sh
ollama stop gemma3:12b 2>/dev/null   # free the GPU

python scripts/4_train.py --name v3_r16_lr2e-4_seed1 --seed 1 \
  2>&1 | tee results/$(date +%F_%H%M)_4_train_seed1.txt
python scripts/5_eval_val.py --skip-base --run outputs/v3_r16_lr2e-4_seed1 --checkpoints 72,144,216 \
  --out results/$(date +%F_%H%M)_5_eval_val_seed1_answers.jsonl \
  2>&1 | tee results/$(date +%F_%H%M)_5_eval_val_seed1.txt

python scripts/4_train.py --name v3_r16_lr1e-4_ep2 --lr 1e-4 --epochs 2 \
  2>&1 | tee results/$(date +%F_%H%M)_4_train_lr1e-4_ep2.txt
python scripts/5_eval_val.py --skip-base --run outputs/v3_r16_lr1e-4_ep2 --checkpoints 72,144 \
  --out results/$(date +%F_%H%M)_5_eval_val_lr1e-4_ep2_answers.jsonl \
  2>&1 | tee results/$(date +%F_%H%M)_5_eval_val_lr1e-4_ep2.txt

echo "ALL DONE $(date)"
