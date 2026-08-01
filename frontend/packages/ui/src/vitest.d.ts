/**
 * Makes the jest-dom matchers (`toBeInTheDocument`, `toBeDisabled`, …) visible
 * to `tsc`. The runtime registration happens in the root `vitest.setup.ts`;
 * this file exists only so type-checking the package sees the augmentation.
 */
import '@testing-library/jest-dom/vitest';
