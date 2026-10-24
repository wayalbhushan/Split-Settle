import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { fetchApi } from '../api';

function GroupDetail() {
  const { id } = useParams();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    const loadData = async () => {
      try {
        const [groupData, expenses, balances, settlements] = await Promise.all([
          fetchApi(`/groups/${id}/`),
          fetchApi(`/groups/${id}/expenses/`),
          fetchApi(`/groups/${id}/balances/`),
          fetchApi(`/groups/${id}/settlements/`)
        ]);

        setData({
          group: groupData,
          expenses,
          balances,
          settlements
        });
      } catch (err) {
        setError(err.message);
      }
    };
    loadData();
  }, [id, refreshKey]);

  const handleDeleteExpense = async (expenseId) => {
    if (window.confirm("Are you sure you want to delete this expense?")) {
      try {
        await fetchApi(`/groups/${id}/expenses/${expenseId}/`, { method: 'DELETE' });
        setRefreshKey(k => k + 1); // trigger refetch
      } catch (err) {
        alert("Failed to delete: " + err.message);
      }
    }
  };

  if (error) return <div className="error-banner">{error}</div>;
  if (!data) return <div>Loading...</div>;

  const { group, expenses, balances, settlements } = data;

  const totalSpent = expenses.reduce((sum, e) => sum + parseFloat(e.amount), 0).toFixed(2);
  const settledStatus = expenses.length === 0 
    ? "0 settlements" 
    : (settlements.length === 0 ? "Fully settled" : `${settlements.length} outstanding settlements`);

  return (
    <div>
      <h2 className="serif" style={{ marginBottom: '0.5rem' }}>{group.name}</h2>
      
      <div style={{ display: 'flex', gap: '1.5rem', marginBottom: '2rem', paddingBottom: '1rem', borderBottom: '1px solid var(--border-color)', fontSize: '0.95rem' }}>
        <div><strong>Total Spent:</strong> <span className="mono">₹{totalSpent}</span></div>
        <div><strong>Expenses:</strong> <span className="mono">{expenses.length}</span></div>
        <div><strong>Members:</strong> <span className="mono">{group.members ? group.members.length : balances.length}</span></div>
        <div><strong>Status:</strong> <span>{settledStatus}</span></div>
      </div>
      
      <section style={{ marginBottom: '3rem' }}>
        <h3 className="serif">Settlements</h3>
        {settlements.length === 0 ? (
          <p>All settled up!</p>
        ) : (
          <ul className="settlement-list">
            {settlements.map((s, i) => (
              <li key={i} className="settlement-item">
                <span>{s.from_member_name} <span className="settlement-arrow">→</span> {s.to_member_name}</span>
                <span className="mono">₹{s.amount}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section style={{ marginBottom: '3rem' }}>
        <div className="flex-between">
          <h3 className="serif">Balances</h3>
          <Link to={`/groups/${id}/members/new`}><button className="secondary">Add Member</button></Link>
        </div>
        <table key={refreshKey} className="flash-row">
          <thead>
            <tr>
              <th>Member</th>
              <th className="text-right">Net Balance</th>
            </tr>
          </thead>
          <tbody>
            {balances.map(b => {
              const val = parseFloat(b.balance);
              const isPos = val > 0;
              const isNeg = val < 0;
              let className = "amount";
              if (isPos) className += " positive";
              if (isNeg) className += " negative";
              
              return (
                <tr key={b.member_id}>
                  <td>
                    <Link to={`/groups/${id}/members/${b.member_id}`} style={{ color: 'var(--text-primary)', textDecoration: 'underline' }}>
                      {b.member_name}
                    </Link>
                  </td>
                  <td className={className}>₹{b.balance}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </section>

      <section>
        <div className="flex-between">
          <h3 className="serif">Expenses</h3>
          <Link to={`/groups/${id}/expenses/new`}><button>Add Expense</button></Link>
        </div>
        {expenses.length === 0 ? (
          <p>No expenses logged yet.</p>
        ) : (
          <table key={refreshKey} className="flash-row">
            <thead>
              <tr>
                <th>Date</th>
                <th>Description</th>
                <th>Paid By</th>
                <th className="text-right">Amount</th>
                <th className="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {expenses.map(e => (
                <tr key={e.id}>
                  <td>{new Date(e.created_at).toLocaleDateString()}</td>
                  <td>{e.description}</td>
                  <td>{e.paid_by_name}</td>
                  <td className="amount">₹{e.amount}</td>
                  <td className="text-right">
                    <Link to={`/groups/${id}/expenses/${e.id}/edit`} className="member-link" style={{ marginRight: '1rem', textDecoration: 'none' }}>Edit</Link>
                    <button onClick={() => handleDeleteExpense(e.id)} className="btn-danger" style={{ padding: '0.25rem 0.5rem', fontSize: '0.9rem' }}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

export default GroupDetail;
