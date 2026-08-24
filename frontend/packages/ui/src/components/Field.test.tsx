import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Input, Select } from './Field';

/**
 * Direction on a field that reads left-to-right inside a right-to-left page.
 *
 * A phone number, an email and a password are all `dir="ltr"` on a page that
 * is otherwise RTL. The icon is positioned against the field's wrapper with a
 * logical property (`start-md`) and the control reserves room for it with
 * another (`ps-[44px]`) — which only line up while both elements resolve
 * `start` to the same physical side.
 *
 * They did not. The attribute went on the control alone, so the wrapper stayed
 * RTL and pinned the icon to the right while the padding moved to the left:
 * a 44px gap on one side, and the typed value running underneath the icon on
 * the other. It showed up on every sign-in, register and profile screen.
 */
describe('Input direction', () => {
  it('puts the direction on the wrapper as well as the control', () => {
    render(<Input label="شماره موبایل" icon="call" dir="ltr" readOnly value="" />);

    const input = screen.getByLabelText('شماره موبایل');
    expect(input).toHaveAttribute('dir', 'ltr');

    // The box the icon is absolutely positioned against has to agree, or the
    // icon and the space reserved for it end up on opposite sides.
    expect(input.parentElement).toHaveAttribute('dir', 'ltr');
  });

  it('leaves the wrapper alone when the field inherits the page direction', () => {
    render(<Input label="نام" icon="person" readOnly value="" />);

    const input = screen.getByLabelText('نام');
    expect(input).not.toHaveAttribute('dir');
    expect(input.parentElement).not.toHaveAttribute('dir');
  });

  it('applies the same rule to a select, whose chevron sits at the end', () => {
    render(
      <Select label="کد کشور" dir="ltr" defaultValue="ir">
        <option value="ir">+98</option>
      </Select>,
    );

    const select = screen.getByLabelText('کد کشور');
    expect(select).toHaveAttribute('dir', 'ltr');
    expect(select.parentElement).toHaveAttribute('dir', 'ltr');
  });
});
