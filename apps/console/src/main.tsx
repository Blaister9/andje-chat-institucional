import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles.css';

// Sin StrictMode: su doble montaje en desarrollo abre y aborta una segunda
// conexión SignalR, ensuciando la consola del navegador.
createRoot(document.getElementById('root')!).render(<App />);
