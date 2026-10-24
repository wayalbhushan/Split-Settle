import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { fetchApi } from '../api';

function MemberDetail() {
  const { id, member_id } = useParams();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetchApi(`/groups/${id}/members/${member_id}/`)
      .then(setData)
      .catch(err => setError(err.message));
  }, [id, member_id]);

  if (error) return <div className="error-banner">{error}</div>;
  if (!data) return <div>Loading...</div>;

  const { member_name, balance, paid_expenses, owed_expenses } = data;
  const val = parseFloat(balance);
  const isPos = val > 0;
  const isNeg = val < 0;
  let balanceClass = "amount";
  if (isPos) balanceClass += " positive";
  if (isNeg) balanceClass += " negative";

  return (
    <div>
      <div className="flex-between" style={{ marginBottom: '2rem' }}>
        <h2 className="serif" style={{ margin: 0 }}>{member_name}</h2>
        <Link to={`/groups/${id}`} className="serif" style={{ color: 'var(--text-primary)', textDecoration: 'underline' }}>
          &larr; Back to Group
        </Link>
      </div>

      <div style={{ marginBottom: '3rem', padding: '1rem', border: '1px solid var(--border-color)', borderRadius: '4px' }}>
        <h3 className="serif" style={{ margin: '0 0 0.5rem 0' }}>Current Balance</h3>
        <div className={balanceClass} style={{ fontSize: '2rem', textAlign: 'left' }}>
          ₹{balance}
        </div>
      </div>

      <section style={{ marginBottom: '3rem' }}>
        <h3 className="serif">Expenses Paid For</h3>
        {paid_expenses.length === 0 ? (
          <p>{member_name} hasn't paid for any expenses.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Description</th>
                <th className="text-right">Total Amount</th>
              </tr>
            </thead>
            <tbody>
              {paid_expenses.map(e => (
                <tr key={e.id}>
                  <td>{new Date(e.created_at).toLocaleDateString()}</td>
                  <td>{e.description}</td>
                  <td className="amount">₹{e.amount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section>
        <h3 className="serif">Owed In Expenses</h3>
        {owed_expenses.length === 0 ? (
          <p>{member_name} doesn't owe for any expenses.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Description</th>
                <th className="text-right">Their Share</th>
                <th className="text-right">Total Amount</th>
              </tr>
            </thead>
            <tbody>
              {owed_expenses.map(e => (
                <tr key={e.expense_id}>
                  <td>{new Date(e.date).toLocaleDateString()}</td>
                  <td>{e.description}</td>
                  <td className="amount negative">₹{e.share_amount}</td>
                  <td className="amount text-right">₹{e.total_amount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

export default MemberDetail;
