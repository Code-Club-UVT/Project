# What a good tutor question looks like (DRAFT, to be edited by Victor)

Source: Victor's marks on 30 val answers (results/2026-11-05_human_check_30_marks.txt).

## A good question (G)
1. It makes the student look at the place of the bug (the line, the branch, the starting value), so thinking about it leads to the bug.
2. It asks ONE thing (one question mark).
3. It does not say what the bug is and does not give the fix. It must not hint at the right value or the right operator (bad: "what initial value should `a` have?").
4. If it uses an example, the example is concrete and small, and the result shows the problem (good: running "abc" shows that `summ` was never set).
5. It talks about the real code: the line numbers and variable names in the question are correct.

## A bad question (B), with the patterns found
- P1. "Add a print in the loop" when the problem is NOT inside the loop (a wrong or missing starting value; a flipped comparison). The student sees numbers but nothing points at the cause.
- P2. Names the variable AND the missing step ("what value does `x` have before the loop starts?"): that is nearly the fix.
- P3. Asks about the correct branch or line (e.g. the `==` case when the bug is in the `<` branch).
- P4. Asks about the wrong variable or line, or quotes a wrong line number.
- P5. Vague or confusing: "how does the value change?" with no direction; mentions something that is not in the code.
- P6. Two or more questions in one.

## What worked, by bug type
- return_in_loop (5 of 7 good): "when does the function stop executing?" / "what does the `return` on line N do to the loop?"
- deleted_initialization (1 of 8 good): run a tiny concrete input and ask what a variable holds at the end ("abc" -> `summ`). Do NOT ask what value it has "before the loop".
- wrong_initial_value (1 of 6 good): ask what the first pass through the loop produces for a tiny input and whether it matches the expected answer. No print-in-loop. Do not say the value is wrong.
- flipped_comparison (1 of 5 good): a small example where the two compared items are in the order the comparison should handle, and ask whether the `if` is entered. Ask about the buggy branch, not the correct one.
- shortened_range, wrong_remainder, deleted_update: no good example yet (0 of 4). Needs new examples.

## Open points for Victor
- Q15 (G, borderline): "what should the initial value of `inv_count` be" is close to P2. Keep it as a good example or drop it?
- Q3 and Q12 were G with a weak part (wrong line number, confusing wording). Better not to use them as examples.
