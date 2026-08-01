import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Price } from './Price';

describe('Price', () => {
  it('renders the price with the currency unit', () => {
    render(<Price value={350_000} />);
    expect(screen.getByText('۳۵۰,۰۰۰ تومان')).toBeInTheDocument();
  });

  it('shows the original price struck through when discounted', () => {
    const { container } = render(<Price value={350_000} compareAt={420_000} />);
    const struck = container.querySelector('s');
    expect(struck).toBeInTheDocument();
    expect(struck).toHaveTextContent('۴۲۰,۰۰۰');
  });

  it('computes the discount percentage from the two prices', () => {
    // 420,000 → 350,000 is a 16.67% cut, rounded to 17.
    render(<Price value={350_000} compareAt={420_000} />);
    expect(screen.getByText('۱۷٪')).toBeInTheDocument();
  });

  it('shows no discount chrome when compareAt is absent', () => {
    const { container } = render(<Price value={350_000} />);
    expect(container.querySelector('s')).not.toBeInTheDocument();
    expect(screen.queryByText(/٪/)).not.toBeInTheDocument();
  });

  it('ignores a compareAt that is not actually higher', () => {
    // Bad data must not render a "۰٪ off" badge or a pointless strikethrough.
    const { container } = render(<Price value={350_000} compareAt={350_000} />);
    expect(container.querySelector('s')).not.toBeInTheDocument();
    expect(screen.queryByText(/٪/)).not.toBeInTheDocument();
  });

  it('can suppress the discount badge while keeping the struck price', () => {
    const { container } = render(
      <Price value={350_000} compareAt={420_000} showDiscount={false} />,
    );
    expect(container.querySelector('s')).toBeInTheDocument();
    expect(screen.queryByText('۱۷٪')).not.toBeInTheDocument();
  });

  it('renders a free item as zero rather than blank', () => {
    render(<Price value={0} />);
    expect(screen.getByText('۰ تومان')).toBeInTheDocument();
  });
});
