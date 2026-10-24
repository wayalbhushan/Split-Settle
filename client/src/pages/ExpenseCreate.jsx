import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { fetchApi } from '../api';

function ExpenseCreate() {
  const { id } = useParams();
  const navigate = useNavigate();
  
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [paidBy, setPaidBy] = useState('');
  const [splitAmong, setSplitAmong] = useState([]);
  
  const [members, setMembers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    fetchApi(`/groups/${id}/members/`)
      .then(data => {
        setMembers(data);
        // Defaults
        if (data.length > 0) {
          setPaidBy(data[0].id.toString());
          setSplitAmong(data.map(m => m.id));
        }
        setLoading(false);
      })
      .catch(err => {
        setError(err.message);
        setLoading(false);
      });
  }, [id]);

  const handleCheckbox = (memberId) => {
    if (splitAmong.includes(memberId)) {
      setSplitAmong(splitAmong.filter(id => id !== memberId));
    } else {
      setSplitAmong([...splitAmong, memberId]);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    setFieldErrors({});

    try {
      await fetchApi(`/groups/${id}/expenses/`, {
        method: 'POST',
        body: JSON.stringify({
          description,
          amount,
          paid_by: paidBy,
          split_among: splitAmong
        })
      });
      navigate(`/groups/${id}`);
    } catch (err) {
      if (err.data) {
        setFieldErrors(err.data);
      } else {
        setError(err.message);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (loading) return <div>Loading...</div>;

  return (
    <div>
      <h2 className="serif">Add Expense</h2>
      {error && <div className="error-banner">{error}</div>}
      
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Description</label>
          <input 
            type="text" 
            value={description}
            onChange={e => setDescription(e.target.value)}
            required
          />
          {fieldErrors.description && <div className="error-message">{fieldErrors.description.join(', ')}</div>}
        </div>

        <div className="form-group">
          <label>Amount (₹)</label>
          <input 
            type="number" 
            step="0.01"
            value={amount}
            onChange={e => setAmount(e.target.value)}
            required
          />
          {fieldErrors.amount && <div className="error-message">{fieldErrors.amount.join(', ')}</div>}
        </div>

        <div className="form-group">
          <label>Paid By</label>
          <select value={paidBy} onChange={e => setPaidBy(e.target.value)} required>
            {members.map(m => (
              <option key={m.id} value={m.id}>{m.name}</option>
            ))}
          </select>
          {fieldErrors.paid_by && <div className="error-message">{fieldErrors.paid_by.join(', ')}</div>}
        </div>

        <div className="form-group">
          <label>Split Among</label>
          <div className="checkbox-list">
            {members.map(m => (
              <label key={m.id} className="checkbox-label">
                <input 
                  type="checkbox"
                  checked={splitAmong.includes(m.id)}
                  onChange={() => handleCheckbox(m.id)}
                />
                {m.name}
              </label>
            ))}
          </div>
          {fieldErrors.split_among && <div className="error-message">{fieldErrors.split_among.join(', ')}</div>}
        </div>

        <div className="flex-between" style={{ justifyContent: 'flex-start', gap: '1rem' }}>
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Saving...' : 'Save Expense'}
          </button>
          <Link to={`/groups/${id}`} className="serif member-link" style={{ textDecoration: 'none' }}>Cancel</Link>
        </div>
      </form>
    </div>
  );
}

export default ExpenseCreate;
