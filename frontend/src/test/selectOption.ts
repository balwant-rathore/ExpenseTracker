import { fireEvent, screen } from '@testing-library/react'

/**
 * Opens a Base UI Select via its labelled trigger and picks the option with
 * the given accessible name. Base UI's Select.Item commits selection based
 * on pointer interaction state, not a bare synthetic `click` event, so a
 * `pointerdown` must fire immediately before the `click` in jsdom.
 */
export async function selectOption(triggerLabel: string, optionName: string) {
  fireEvent.click(screen.getByLabelText(triggerLabel))
  const option = await screen.findByRole('option', { name: optionName })
  fireEvent.pointerDown(option, { button: 0, pointerId: 1 })
  fireEvent.click(option)
}
