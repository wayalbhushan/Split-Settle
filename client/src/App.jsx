import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import GroupList from './pages/GroupList';
import GroupCreate from './pages/GroupCreate';
import GroupDetail from './pages/GroupDetail';
import MemberCreate from './pages/MemberCreate';
import ExpenseCreate from './pages/ExpenseCreate';
import ExpenseEdit from './pages/ExpenseEdit';
import MemberDetail from './pages/MemberDetail';

function App() {
  return (
    <Router>
      <div className="container">
        <header className="nav-header flex-between">
          <h1 className="serif" style={{ margin: 0 }}>
            <Link to="/">SplitLedger</Link>
          </h1>
        </header>
        
        <Routes>
          <Route path="/" element={<GroupList />} />
          <Route path="/groups/new" element={<GroupCreate />} />
          <Route path="/groups/:id" element={<GroupDetail />} />
          <Route path="/groups/:id/members/new" element={<MemberCreate />} />
          <Route path="/groups/:id/members/:member_id" element={<MemberDetail />} />
          <Route path="/groups/:id/expenses/new" element={<ExpenseCreate />} />
          <Route path="/groups/:id/expenses/:expense_id/edit" element={<ExpenseEdit />} />
        </Routes>
      </div>
    </Router>
  );
}

export default App;
