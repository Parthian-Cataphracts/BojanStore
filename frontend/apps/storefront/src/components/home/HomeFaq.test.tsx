// Imported here as well as in `vitest.setup.ts`: the setup file is outside
// this app's tsconfig, so without this the matchers run but do not typecheck.
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { HomeFaq } from './HomeFaq';
import type { Faq } from '@/lib/api/pages';

/**
 * The home page's FAQ accordion.
 *
 * Two things here are worth a test rather than a look. The first is that
 * nothing is open on arrival — unlike `/faq`, where the first answer is the
 * point of the page. Here an answer expanded by default pushes everything below
 * it down by a paragraph nobody asked for, on the page most likely to be
 * measured for layout shift, and "the first one is open" is exactly the kind of
 * default that gets restored by accident.
 *
 * The second is that only one answer is open at a time, which is the difference
 * between an accordion and a list of paragraphs with arrows on them.
 */

const shipping: Faq = {
  question: 'زمان ارسال چقدر است؟',
  answer: 'بین ۲ تا ۵ روز کاری.',
  category: 'ارسال',
};

const returns: Faq = {
  question: 'امکان مرجوعی هست؟',
  answer: 'تا ۷ روز پس از دریافت کالا.',
  category: 'مرجوعی',
};

// Named rather than indexed out of the array: `noUncheckedIndexedAccess` is on,
// and `faqs[0]!` in five places reads as noise around the thing being tested.
const faqs: Faq[] = [shipping, returns];

describe('HomeFaq', () => {
  it('starts with every answer collapsed', () => {
    render(<HomeFaq items={faqs} />);

    for (const faq of faqs) {
      expect(screen.getByRole('button', { name: faq.question })).toHaveAttribute(
        'aria-expanded',
        'false',
      );
      expect(screen.queryByText(faq.answer)).not.toBeInTheDocument();
    }
  });

  it('opens the answer that was clicked', async () => {
    const user = userEvent.setup();
    render(<HomeFaq items={faqs} />);

    await user.click(screen.getByRole('button', { name: shipping.question }));

    expect(screen.getByText(shipping.answer)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: shipping.question })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
  });

  it('closes the open answer when its own question is clicked again', async () => {
    const user = userEvent.setup();
    render(<HomeFaq items={faqs} />);

    const question = screen.getByRole('button', { name: shipping.question });
    await user.click(question);
    await user.click(question);

    expect(screen.queryByText(shipping.answer)).not.toBeInTheDocument();
  });

  it('keeps only one answer open at a time', async () => {
    const user = userEvent.setup();
    render(<HomeFaq items={faqs} />);

    await user.click(screen.getByRole('button', { name: shipping.question }));
    await user.click(screen.getByRole('button', { name: returns.question }));

    expect(screen.queryByText(shipping.answer)).not.toBeInTheDocument();
    expect(screen.getByText(returns.answer)).toBeInTheDocument();
  });

  /**
   * The button points at its answer by id. Without it a screen reader announces
   * the question as expanded and has nothing to say it expanded into.
   */
  it('points each question at the answer it controls', async () => {
    const user = userEvent.setup();
    render(<HomeFaq items={faqs} />);

    const question = screen.getByRole('button', { name: shipping.question });
    await user.click(question);

    const id = question.getAttribute('aria-controls');
    expect(id).toBeTruthy();
    expect(document.getElementById(id!)).toHaveTextContent(shipping.answer);
  });
});
