import { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle, RotateCcw } from 'lucide-react';
import { Button } from './ui/Button';

interface Props {
  children: ReactNode;
  fallbackTitle?: string;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public override state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public override componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('ErrorBoundary atrapó un error en el árbol de componentes:', error, errorInfo);
  }

  private handleReset = () => {
    this.setState({ hasError: false, error: null });
    window.location.reload();
  };

  public override render() {
    if (this.state.hasError) {
      return (
        <div className="min-h-[400px] flex flex-col items-center justify-center p-8 text-center max-w-lg mx-auto bg-white rounded-2xl border border-rose-200 shadow-sm my-8">
          <div className="w-14 h-14 bg-rose-50 rounded-2xl flex items-center justify-center mb-4 text-rose-600 border border-rose-100">
            <AlertTriangle className="w-7 h-7" aria-hidden="true" />
          </div>

          <h3 className="text-lg font-bold text-slate-900 mb-2">
            {this.props.fallbackTitle || 'Ocurrió un error inesperado en la interfaz'}
          </h3>

          <p className="text-sm text-slate-500 mb-6 leading-relaxed">
            Se ha producido una excepción al procesar la vista. Tus datos en el servidor no han sido alterados.
          </p>

          <Button
            variant="primary"
            onClick={this.handleReset}
            leftIcon={<RotateCcw className="w-4 h-4" />}
          >
            Recargar Aplicación
          </Button>
        </div>
      );
    }

    return this.props.children;
  }
}
