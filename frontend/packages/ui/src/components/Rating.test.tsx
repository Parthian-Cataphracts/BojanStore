import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Rating } from './Rating';

describe('Rating', () => {
  it('renders five stars', () => {
    const { container } = render(<Rating value={4} />);
    expect(container.querySelectorAll('.material-symbols-outlined')).toHaveLength(5);
  });

  it('exposes the score to screen readers, since the stars are decorative', () => {
    render(<Rating value={4.2} />);
    expect(screen.getByLabelText('4.2 از ۵')).toBeInTheDocument();
  });

  it('rounds the label to one decimal place', () => {
    render(<Rating value={4.26} />);
    expect(screen.getByLabelText('4.3 از ۵')).toBeInTheDocument();
  });

  it('shows the review count in Persian digits when given', () => {
    render(<Rating value={4.5} count={124} />);
    expect(screen.getByText(/۱۲۴ نظر/)).toBeInTheDocument();
  });

  it('omits the review count when it is not given', () => {
    render(<Rating value={4.5} />);
    expect(screen.queryByText(/نظر/)).not.toBeInTheDocument();
  });

  it('renders a single star and the numeric score in compact mode', () => {
    const { container } = render(<Rating value={4.8} compact />);
    expect(container.querySelectorAll('.material-symbols-outlined')).toHaveLength(1);
    expect(screen.getByText('۴.۸')).toBeInTheDocument();
  });

  it('handles the zero-rating edge without rendering filled stars', () => {
    render(<Rating value={0} />);
    expect(screen.getByLabelText('0 از ۵')).toBeInTheDocument();
  });
});
