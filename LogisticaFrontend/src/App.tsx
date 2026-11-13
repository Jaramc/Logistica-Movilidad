import { QueryClientProvider } from '@tanstack/react-query';
import { SesionProvider } from './features/auth/SesionProvider';
import { PuertaDeAcceso } from './features/auth/components/PuertaDeAcceso';
import { crearQueryClient } from './lib/queryClient';

const queryClient = crearQueryClient();

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <SesionProvider>
        <PuertaDeAcceso />
      </SesionProvider>
    </QueryClientProvider>
  );
}

export default App;
