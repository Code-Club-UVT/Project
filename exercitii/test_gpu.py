import time
import torch

n = 4096
a = torch.randn(n, n)
b = torch.randn(n, n)

t = time.time()
a @ b
print("CPU:", round(time.time() - t, 4), "secunde")

a = a.to("cuda")
b = b.to("cuda")
a @ b
torch.cuda.synchronize()

t = time.time()
for _ in range(10):
    a @ b
torch.cuda.synchronize()
print("GPU (medie din 10):", round((time.time() - t) / 10, 5), "secunde")
