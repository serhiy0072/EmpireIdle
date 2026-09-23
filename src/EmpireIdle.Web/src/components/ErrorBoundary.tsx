import { Component, type ErrorInfo, type ReactNode } from "react";

interface Props {
  children: ReactNode;
}

interface State {
  failed: boolean;
}

/**
 * Падіння рендеру замість білого екрана показує пояснення й кнопку.
 * Класовий компонент, бо хуком помилку рендеру не зловити.
 * Скидається зміною key ззовні — так перехід на іншу вкладку повертає екран.
 */
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Екран упав під час рендеру", error, info.componentStack);
  }

  render() {
    if (!this.state.failed) {
      return this.props.children;
    }

    return (
      <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
        <p className="font-medium">Цей екран не вдалося показати.</p>
        <p className="mt-1">Перейдіть на іншу вкладку або оновіть сторінку. Прогрес збережено на сервері.</p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="mt-3 rounded-lg border border-rose-300 bg-white px-3 py-1 text-rose-800 hover:bg-rose-100"
        >
          Оновити сторінку
        </button>
      </div>
    );
  }
}
