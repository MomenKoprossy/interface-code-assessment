import type { PayItem, Payslip } from '../../api/types';
import { formatMoney, formatMonth, formatRate } from '../../shared/format';

/** Read-only breakdown. Every number comes from the server; nothing is computed here. */
export function PayslipView({ payslip }: { payslip: Payslip }) {
  const money = (amount: number) => formatMoney(amount, payslip.currency);
  const si = payslip.socialInsurance;

  return (
    <article className="payslip card" aria-labelledby="payslip-title">
      <header className="payslip__header">
        <div>
          <h2 id="payslip-title" className="payslip__name">
            {payslip.employeeName}
          </h2>
          <p className="payslip__meta">
            {payslip.jobTitle} · {payslip.department}
          </p>
        </div>
        <p className="payslip__month">{formatMonth(payslip.month)}</p>
      </header>

      <table className="payslip__table">
        <caption className="visually-hidden">Payslip breakdown for {formatMonth(payslip.month)}</caption>
        <tbody>
          <SectionRow title="Earnings" />
          <AmountRow label="Base salary" amount={money(payslip.baseSalary)} />
          <ItemRows items={payslip.allowances} money={money} />
          <AmountRow label="Gross pay" amount={money(payslip.grossPay)} total />
        </tbody>
        <tbody>
          <SectionRow title="Deductions" />
          <AmountRow
            label={`Social insurance (${formatRate(si.rate)} of base salary)`}
            amount={money(si.amount)}
            note={
              si.capApplied && si.monthlyCap !== null
                ? `Capped at ${money(si.monthlyCap)}; uncapped it would be ${money(si.uncappedAmount)}.`
                : undefined
            }
          />
          <AmountRow
            label="Income tax"
            amount={money(payslip.incomeTax.amount)}
            note={`On taxable income of ${money(payslip.taxableIncome)}.`}
          />
          <ItemRows items={payslip.otherDeductions} money={money} />
          <AmountRow label="Total deductions" amount={money(payslip.totalDeductions)} total />
        </tbody>
        <tfoot>
          <tr className="payslip__net">
            <th scope="row">Net pay</th>
            <td>{money(payslip.netPay)}</td>
          </tr>
        </tfoot>
      </table>

      <section className="payslip__tax" aria-labelledby="tax-brackets-title">
        <h3 id="tax-brackets-title" className="payslip__subtitle">
          Income tax by bracket
        </h3>
        {payslip.incomeTax.brackets.length === 0 ? (
          <p>No tax brackets apply.</p>
        ) : (
          <div className="table-scroll">
            <table className="data-table data-table--compact" aria-labelledby="tax-brackets-title">
              <thead>
                <tr>
                  <th scope="col">Bracket</th>
                  <th scope="col" className="data-table__number">
                    Rate
                  </th>
                  <th scope="col" className="data-table__number">
                    Taxed amount
                  </th>
                  <th scope="col" className="data-table__number">
                    Tax
                  </th>
                </tr>
              </thead>
              <tbody>
                {payslip.incomeTax.brackets.map((bracket) => (
                  <tr key={bracket.from}>
                    <td data-label="Bracket">
                      {/* One element (a single flex item in the narrow card), wrapping only between its two halves. */}
                      <span>
                        {bracket.upTo === null ? (
                          `Over ${money(bracket.from)}`
                        ) : bracket.from === 0 ? (
                          `Up to ${money(bracket.upTo)}`
                        ) : (
                          <>
                            <span className="data-table__nowrap">Over {money(bracket.from)}</span>{' '}
                            <span className="data-table__nowrap">up to {money(bracket.upTo)}</span>
                          </>
                        )}
                      </span>
                    </td>
                    <td data-label="Rate" className="data-table__number">
                      {formatRate(bracket.rate)}
                    </td>
                    <td data-label="Taxed amount" className="data-table__number">
                      {money(bracket.taxedAmount)}
                    </td>
                    <td data-label="Tax" className="data-table__number">
                      {money(bracket.tax)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <p className="payslip__footnote">Rounding: {payslip.roundingRule}</p>
    </article>
  );
}

function SectionRow({ title }: { title: string }) {
  return (
    <tr className="payslip__section">
      <th scope="rowgroup" colSpan={2}>
        {title}
      </th>
    </tr>
  );
}

function AmountRow({ label, amount, note, total }: { label: string; amount: string; note?: string; total?: boolean }) {
  return (
    <tr className={total ? 'payslip__total' : undefined}>
      <th scope="row">
        {label}
        {note && <span className="payslip__note">{note}</span>}
      </th>
      <td>{amount}</td>
    </tr>
  );
}

function ItemRows({ items, money }: { items: PayItem[]; money: (amount: number) => string }) {
  return items.map((item) => <AmountRow key={item.name} label={item.name} amount={money(item.amount)} />);
}
