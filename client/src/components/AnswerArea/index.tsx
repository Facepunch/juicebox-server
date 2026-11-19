import React, { useMemo } from "react";
import { FormModel, FormModelProvider } from "../../models/FormModel";
import { useGameModel } from "../../models/GameModel";
import { JuiceboxForm } from "../../models/JuiceboxInterfaces";
import FormContainer from "./FormContainer";
import Button from "../Button";
import Control from "./Control";

interface Props {
    readonly form: JuiceboxForm;
    readonly disabled: boolean;
}

const Form: React.FC<Props> = ({ form, disabled }) => {
    const gameModel = useGameModel();
    // eslint-disable-next-line react-hooks/exhaustive-deps
    const formModel = useMemo(() => new FormModel(), [form]);

    if (!Array.isArray(form?.Controls)) {
        return null;
    }

    const formInputs = form.Controls.filter((c) => c.Type !== "Button");
    const actionInputs = form.Controls.filter((c) => c.Type === "Button");
    const needsSubmitButton = formInputs.length > 0;

    return (
        <FormModelProvider value={formModel}>
            <FormContainer>
                {formInputs.map((c, i) => (
                    <Control
                        key={`form-${i}-${c.Key}`}
                        control={c}
                        disabled={disabled}
                    />
                ))}
                {needsSubmitButton && (
                    <Button
                        label={form?.SubmitLabel ?? "Submit"}
                        disabled={disabled}
                        onClick={async () =>
                            gameModel.sendResponse(await formModel.capture())
                        }
                    />
                )}
                {formInputs.length > 0 && actionInputs.length > 0 && <hr />}
                {actionInputs.map((c, i) => (
                    <Control
                        key={`action-${i}-${c.Key}`}
                        control={c}
                        disabled={false}
                    />
                ))}
            </FormContainer>
        </FormModelProvider>
    );
};

export default Form;
