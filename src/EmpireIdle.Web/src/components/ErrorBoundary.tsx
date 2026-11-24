import { Component, type ErrorInfo, type ReactNode } from "react";
import { t } from "i18next";

interface Props {
  children: ReactNode;
  /** Зміна значення повертає впалий екран до життя; здоровий екран вона не чіпає. */
  resetKey: string;
}

interface State {
  failed: boolean;
}

/**
 * Падіння рендеру замість білого екрана показує пояснення й кнопку.
 * Класовий компонент, бо хуком помилку рендеру не зловити.
 * Скидається зміною resetKey — так перехід на іншу адресу повертає екран. Не через key:
 * key перемонтовував би й здорову сторінку на кожній зміні вкладки всередині неї.
 */
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidUpdate(previous: Props) {
    if (this.state.failed && previous.resetKey !== this.props.resetKey) {
      this.setState({ failed: false });
    }
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
        <p className="font-medium">{t("errors:boundary.title")}</p>
        <p className="mt-1">{t("errors:boundary.text")}</p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="mt-3 rounded-lg border border-rose-300 bg-white px-3 py-1 text-rose-800 hover:bg-rose-100"
        >
          {t("errors:boundary.reload")}
        </button>
      </div>
    );
  }
}
