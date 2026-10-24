import { useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { fetchApi } from '../api';

function MemberCreate() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    setFieldErrors({});

    try {
      await fetchApi(`/groups/${id}/members/`, {
        method: 'POST',
        body: JSON.stringify({ name })
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

  return (
    <div>
      <h2 className="serif">Add Member</h2>
      {error && <div className="error-banner">{error}</div>}
      
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Member Name</label>
          <input 
            type="text" 
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
          {fieldErrors.name && <div className="error-message">{fieldErrors.name.join(', ')}</div>}
        </div>

        <div className="flex-between" style={{ justifyContent: 'flex-start', gap: '1rem' }}>
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Saving...' : 'Save Member'}
          </button>
          <Link to={`/groups/${id}`} className="serif member-link" style={{ textDecoration: 'none' }}>Cancel</Link>
        </div>
      </form>
    </div>
  );
}

export default MemberCreate;
