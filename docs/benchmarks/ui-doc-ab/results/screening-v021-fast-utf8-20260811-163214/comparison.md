# ui_doc Authoring A/B Comparison

Wave: screening-v021-fast-utf8-20260811-163214
Protocol: fast

Decision: **inconclusive**

Valid runs: 12 / 12

| Arm | Strict | Mean score | Median agent ms | Mean calls | Mean input tokens |
|---|---:|---:|---:|---:|---:|
| uidoc | 0/6 | 25.406 | 240168.31 | 17.833 | 0 |
| primitives_batch | 0/6 | 74.017 | 240180.166 | 26.333 | 0 |

Best generic arm: **primitives_batch**

Overall uidoc advantage: **-48.611 points**

| Task | uidoc mean | generic mean | uidoc advantage | uidoc strict | generic strict |
|---|---:|---:|---:|---:|---:|
| T01 | 44.166 | 94 | -49.834 | 0 | 0 |
| T02 | 14.75 | 36.05 | -21.3 | 0 | 0 |
| T03 | 17.3 | 92 | -74.7 | 0 | 0 |
