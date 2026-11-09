# Payroll rules

> The core business logic. Implemented as pure code in `backend/src/Modules/Payroll/Payroll`
> (no EF, no ASP.NET). Configured by the `Payroll` section of `appsettings.json`; nothing here is hard-coded.

## Formula (one month, one employee)

```
gross            = baseSalary + Σ allowances
socialInsurance  = min( round(baseSalary × siRate), monthlyCap )        // cap optional (null = no cap)
taxableIncome    = gross − socialInsurance   (if DeductSocialInsuranceBeforeTax)   else gross
incomeTax        = Σ over brackets: round( portion of taxableIncome inside the bracket × bracket rate )
otherDeductions  = Σ employee's recurring deductions
totalDeductions  = socialInsurance + incomeTax + otherDeductions
net              = gross − totalDeductions
```

## Configuration (default values, `appsettings.json` → `Payroll`)

```json
"Payroll": {
  "Currency": "USD",
  "DeductSocialInsuranceBeforeTax": true,
  "SocialInsurance": { "Rate": 0.11, "MonthlyCap": 1100.00 },
  "TaxBrackets": [
    { "UpTo": 1500.00,  "Rate": 0.00 },
    { "UpTo": 5000.00,  "Rate": 0.10 },
    { "UpTo": 10000.00, "Rate": 0.20 },
    { "UpTo": null,     "Rate": 0.25 }
  ]
}
```

Validated at **startup** (`ValidateOnStart`), so a bad policy stops the app instead of producing wrong payslips:
at least one bracket; `UpTo` strictly ascending; only the **last** bracket has `UpTo = null`; every rate in [0, 1];
SI rate in [0, 1]; `MonthlyCap` null or > 0; currency is 3 letters.

## Bracket semantics

- Brackets are **monthly** amounts applied to **taxable income**.
- Bracket *i* covers `(UpTo[i-1], UpTo[i]]`, where the first bracket starts at 0. **Upper bounds are inclusive**:
  income exactly on a boundary is taxed entirely at the lower bracket's rate, and the next bracket starts
  taxing from the first cent above it.
- Each bracket's tax is the **portion** inside it × its rate (progressive, not "whole income at the top rate").

## Rounding rule (documented choice)

- All stored amounts (salary, allowances, deductions) have at most 2 decimals; the validators enforce this.
- **Every monetary line on the payslip is rounded to 2 decimals, half away from zero**
  (`MidpointRounding.AwayFromZero`, "commercial rounding": 0.005 → 0.01) at the moment it's computed. That covers
  each bracket's tax and the social insurance amount.
- **Totals are sums of rounded lines.** So the payslip always adds up to the cent: what the employee
  sees line by line equals the totals. The trade-off: total tax can differ by at most ½ cent per bracket from
  "round once at the end". We accept that for auditability.
- Arithmetic uses `decimal` (base-10, 28 significant digits). Never `double`/`float`: binary floating point can't
  represent 0.1 exactly, which is unacceptable for money.

## Edge cases (each one has a unit test)

| Case | Behaviour |
|---|---|
| Inactive employee | 422 `Payslip is not available for an inactive employee.` |
| Month before the hire month | 422 `Employee was not yet hired in {month}.` |
| Hired mid-month | Full month's pay. **Assumption: no proration** (documented limitation) |
| Zero allowances / zero deductions | Valid, with empty lists and 0 totals |
| Taxable income exactly on a bracket boundary | Taxed fully at the lower bracket |
| Taxable income 0 / below the first bracket | Tax 0 |
| SI above the cap | Capped. `capApplied = true`, and `uncappedAmount` is shown |
| No cap configured | `monthlyCap = null`, never capped |
| Deductions exceed gross (net would be negative) | 422 `Deductions exceed gross pay for {month}.`. A payslip is never issued with a negative net |
| Malformed or missing `month` | 400 validation problem on `month` |
| Future month | Allowed (payroll preview) |
| Midpoint rounding (e.g. 0.005) | Rounds away from zero |

## Extensibility: the rule pipeline

The calculator runs an **ordered list of `IPayrollRule`s** over a `PayslipDraft`:

1. `GrossEarningsRule`: base + allowances
2. `SocialInsuranceRule`: rate × base, capped
3. `IncomeTaxRule`: progressive brackets over taxable income
4. `OtherDeductionsRule`: the employee's recurring deductions

Each rule only reads earlier results and writes its own lines. **Adding a deduction type** (pension, health
insurance, union dues) means one new class implementing `IPayrollRule` plus registering it with an order. The
calculator and the other rules stay unchanged (Open/Closed). Tax rules are data (config), so changing brackets is a
config change, not a code change. A future per-country policy is a different `PayrollPolicy` instance, selected
per employee.

## Assumptions

- Single currency, configured globally and used for display only.
- Monthly payroll. Brackets are monthly, not annual.
- Allowances are fixed monthly amounts and fully taxable. "Other deductions" are post-tax recurring deductions.
- Payslips are computed on demand and not stored (no payroll run, locking or history). That's a known limitation.
