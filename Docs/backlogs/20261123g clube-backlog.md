# Backlog 2026-11-23g

Playtest note on the demo build made after X26-X38, in the user's own words:

1. Also bug with selling to the merchant at 0 disposition, it lists items to sell at 0 value which means there is no way to increase disposition when it's at 0 and it says you see 9.99c traded to raise disposition by 1. Make sure the formula is being followed correctly as round(5.619*exp(0.057575*current_disposition) ) should actually give you a value of selling 6c worth of goods to raise disposition by 1 if you are at 0. Also fix the logic on selling items so you get >0c when the disposition is at 0. Provide me with the formula used here so I can look as well
