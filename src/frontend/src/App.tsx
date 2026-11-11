import { EventsList } from './features/events/EventsList';
import './App.css';

function App() {
  return (
    <div className="app-container">
      <header className="app-header">
        <h1 className="app-title">Seating Planner</h1>
        <p className="app-subtitle">Plan who sits where at your events</p>
      </header>

      <main className="main-content">
        <EventsList />
      </main>
    </div>
  );
}

export default App;
