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

  describe('with onRemove', () => {
    it('turns the decrement into a remove at the minimum', async () => {
      // Where the stepper is the whole control — the product page — a minus
      // that goes dead at one leaves no way back out of the basket.
      const onRemove = vi.fn();
      const onChange = vi.fn();
      render(<QuantityStepper value={1} onChange={onChange} onRemove={onRemove} />);

      const remove = screen.getByRole('button', { name: 'حذف از سبد خرید' });
      expect(remove).toBeEnabled();

      await userEvent.click(remove);
      expect(onRemove).toHaveBeenCalledTimes(1);
      // Removing is not decrementing to zero.
      expect(onChange).not.toHaveBeenCalled();
    });

    it('is an ordinary decrement above the minimum', async () => {
      // Taking one off a line of three is not removing anything.
      const onRemove = vi.fn();
      const onChange = vi.fn();
      render(<QuantityStepper value={3} onChange={onChange} onRemove={onRemove} />);

      expect(screen.queryByRole('button', { name: 'حذف از سبد خرید' })).toBeNull();
      await userEvent.click(screen.getByRole('button', { name: 'کاهش تعداد' }));

      expect(onChange).toHaveBeenCalledWith(2);
      expect(onRemove).not.toHaveBeenCalled();
    });

    it('still goes dead at the minimum when there is nowhere to remove to', () => {
      // No `onRemove` is the cart's own rows, which carry a delete button of
      // their own beside the stepper.
      const { decrease } = setup({ value: 1 });
      expect(decrease).toBeDisabled();
      expect(decrease).toHaveAccessibleName('کاهش تعداد');
    });
  });

  describe('with onStep', () => {
    it('reports the change rather than the result', async () => {
      /*
        `onChange` hands over `value + 1`, worked out from the value this render
        was given, so two presses inside one frame both ask for the same number
        and the second is the first repeated. Tapping «+» five times quickly on
        a product page put three in the basket. A delta cannot say the wrong
        number because it does not say a number.
      */
      const onStep = vi.fn();
      const onChange = vi.fn();
      render(<QuantityStepper value={2} onChange={onChange} onStep={onStep} />);

      await userEvent.click(screen.getByRole('button', { name: 'افزایش تعداد' }));
      expect(onStep).toHaveBeenCalledWith(1);

      await userEvent.click(screen.getByRole('button', { name: 'کاهش تعداد' }));
      expect(onStep).toHaveBeenCalledWith(-1);

      // The absolute path is not taken as well — one press is one instruction.
      expect(onChange).not.toHaveBeenCalled();
    });

    it('still stops at the ceiling stock sets', async () => {
      const onStep = vi.fn();
      render(<QuantityStepper value={5} max={5} onChange={vi.fn()} onStep={onStep} />);

      const increase = screen.getByRole('button', { name: 'افزایش تعداد' });
      expect(increase).toBeDisabled();
      await userEvent.click(increase);
      expect(onStep).not.toHaveBeenCalled();
    });

    it('lets removal win at the minimum', async () => {
      // Both are configured on the product page's control; at one the trash is
      // the decrement, and stepping to zero is not a thing it may ask for.
      const onStep = vi.fn();
      const onRemove = vi.fn();
      render(
        <QuantityStepper value={1} onChange={vi.fn()} onStep={onStep} onRemove={onRemove} />,
      );

      await userEvent.click(screen.getByRole('button', { name: 'حذف از سبد خرید' }));
      expect(onRemove).toHaveBeenCalledTimes(1);
      expect(onStep).not.toHaveBeenCalled();
    });
  });
});
