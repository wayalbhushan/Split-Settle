import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { fetchApi } from '../api';

function GroupList() {
  const [groups, setGroups] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchApi('/groups/')
      .then(data => {
        setGroups(data);
        setLoading(false);
      })
      .catch(err => {
        setError(err.message);
        setLoading(false);
      });
  }, []);

  if (loading) return <div>Loading...</div>;
  if (error) return <div className="error-banner">{error}</div>;

  return (
    <div>
      <div className="flex-between" style={{ marginBottom: '1.5rem' }}>
        <h2 className="serif" style={{ margin: 0 }}>Groups</h2>
        <Link to="/groups/new">
          <button>Create New Group</button>
        </Link>
      </div>
      
      {groups.length === 0 ? (
        <p>No groups logged yet.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Members</th>
            </tr>
          </thead>
          <tbody>
            {groups.map(group => (
              <tr key={group.id}>
                <td>
                  <Link to={`/groups/${group.id}`}>{group.name}</Link>
                </td>
                <td>{group.member_count}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

export default GroupList;
