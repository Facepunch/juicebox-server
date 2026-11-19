import React, { useCallback } from "react";
import { useGameModel } from "../../models/GameModel";
import { JuiceboxButton } from "../../models/JuiceboxInterfaces";
import Button from "../Button";

interface Props {
    readonly control: JuiceboxButton;
    readonly disabled: boolean;
}

const ButtonControl: React.FC<Props> = ({ control, disabled }) => {
    const gameModel = useGameModel();
    const clickHandler = useCallback(
        () => gameModel.sendAction(control?.Key),
        [gameModel, control]
    );
    return (
        <Button
            label={control?.Label}
            variant={control?.Variant}
            onClick={clickHandler}
            disabled={disabled}
        />
    );
};

export default ButtonControl;
