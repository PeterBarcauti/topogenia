// ErrorPage.tsx
// A page that could not be shown: its message and the one way out (Try again, or Back to home) (B-1.1-10, B-1.2-02).

export interface ErrorPageProps {
  readonly message: string;
  readonly actionLabel: string;
  readonly testId: string;
  readonly onAction: () => void;
}

export function ErrorPage({ message, actionLabel, testId, onAction }: ErrorPageProps) {
  return (
    <div className="page-error" role="alert" data-testid={testId}>
      <p>{message}</p>
      <button type="button" className="btn btn--primary" data-testid={`${testId}-action`} onClick={onAction}>{actionLabel}</button>
    </div>
  );
}
