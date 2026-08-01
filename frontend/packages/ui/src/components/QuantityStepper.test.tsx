import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { QuantityStepper } from './QuantityStepper';

/** Renders a controlled stepper and reports what onChange was called with. */
function setup(props: Partial<React.ComponentProps<typeof QuantityStepper>> = {}) {
  const onChange = vi.fn();
  render(<QuantityStepper value={2} onChange={onChange} {...props} />);
  return {
    onChange,
    increase: screen.getByRole('button', { name: 'افزایش تعداد' }),
    decrease: screen.getByRole('button', { name: 'کاهش تعداد' }),
  };
}

describe('QuantityStepper', () => {
  it('shows the value in Persian digits', () => {
    setup({ value: 12 });
    expect(screen.getByText('۱۲')).toBeInTheDocument();
  });

  it('reports the incremented value', async () => {
    const { onChange, increase } = setup({ value: 2 });
    await userEvent.click(increase);
    expect(onChange).toHaveBeenCalledWith(3);
  });

  it('reports the decremented value', async () => {
    const { onChange, decrease } = setup({ value: 2 });
    await userEvent.click(decrease);
    expect(onChange).toHaveBeenCalledWith(1);
  });

  it('disables decrement at the minimum so quantity cannot reach zero', async () => {
    const { onChange, decrease } = setup({ value: 1, min: 1 });
    expect(decrease).toBeDisabled();
    await userEvent.click(decrease);
    expect(onChange).not.toHaveBeenCalled();
  });

  it('disables increment at the maximum, which is how stock is enforced', async () => {
    const { onChange, increase } = setup({ value: 5, max: 5 });
    expect(increase).toBeDisabled();
    await userEvent.click(increase);
    expect(onChange).not.toHaveBeenCalled();
  });

  it('disables both controls while a cart mutation is in flight', () => {
    const { increase, decrease } = setup({ disabled: true });
    expect(increase).toBeDisabled();
    expect(decrease).toBeDisabled();
  });

  it('never emits a value outside the range even from an out-of-range start', async () => {
    // Guards against a stale value arriving from the server after stock drops.
    const { onChange, increase } = setup({ value: 9, max: 5 });
    expect(increase).toBeDisabled();
    await userEvent.click(increase);
    expect(onChange).not.toHaveBeenCalled();
  });

  it('labels its controls for screen readers', () => {
    const { increase, decrease } = setup();
    expect(increase).toHaveAccessibleName('افزایش تعداد');
    expect(decrease).toHaveAccessibleName('کاهش تعداد');
  });
});
