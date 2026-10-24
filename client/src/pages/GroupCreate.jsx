import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { fetchApi } from '../api';

function GroupCreate() {
  const [name, setName] = useState('');
  const [initialMembers, setInitialMembers] = useState('');
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    setFieldErrors({});

    try {
      const group = await fetchApi('/groups/', {
        method: 'POST',
        body: JSON.stringify({ name }),
      });

      if (initialMembers.trim()) {
        const names = initialMembers.split(',').map(n => n.trim()).filter(n => n);
        // Add members sequentially to avoid parallel issues or just loop
        for (const memberName of names) {
          await fetchApi(`/groups/${group.id}/members/`, {
            method: 'POST',
            body: JSON.stringify({ name: memberName })
          });
        }
      }

      navigate(`/groups/${group.id}`);
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

  return (
    <div>
      <h2 className="serif">Create Group</h2>
      {error && <div className="error-banner">{error}</div>}
      
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Group Name</label>
          <input 
            type="text" 
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
          {fieldErrors.name && <div className="error-message">{fieldErrors.name.join(', ')}</div>}
        </div>

        <div className="form-group">
          <label>Initial Members (comma-separated)</label>
          <input 
            type="text" 
            value={initialMembers}
            onChange={(e) => setInitialMembers(e.target.value)}
            placeholder="e.g. Aarav, Bhavya, Chirag"
          />
        </div>

        <div className="flex-between" style={{ justifyContent: 'flex-start', gap: '1rem' }}>
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Saving...' : 'Save'}
          </button>
          <Link to="/" className="serif member-link" style={{ textDecoration: 'none' }}>Cancel</Link>
        </div>
      </form>
    </div>
  );
}

export default GroupCreate;
