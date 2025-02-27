import { BatteryStatus } from 'models/Battery'
import { tokens } from '@equinor/eds-tokens'
import { Icon, Typography } from '@equinor/eds-core-react'
import styled from 'styled-components'
import { Icons } from 'utils/icons'

const BatteryStatusTile = styled.div`
    display: flex;
    align-items: center;
    justify-content: flex-end;
`

export interface BatteryStatusViewProps {
    battery?: number
    batteryStatus?: BatteryStatus
}

const BatteryStatusView = ({ battery, batteryStatus }: BatteryStatusViewProps): JSX.Element => {
    let battery_icon
    let icon_color: string = tokens.colors.interactive.primary__resting.hex
    let battery_value: string

    if (!battery) {
        battery_value = '---%'
        battery_icon = Icons.BatteryUnknown
    } else {
        battery_value = `${battery}%`
        switch (batteryStatus) {
            case BatteryStatus.Normal:
                battery_icon = Icons.Battery
                break
            case BatteryStatus.Charging:
                battery_icon = Icons.BatteryCharging
                break
            case BatteryStatus.Critical:
                battery_icon = Icons.BatteryAlert
                icon_color = tokens.colors.interactive.danger__resting.hex
                break
            default:
                battery_icon = Icons.BatteryUnknown
                battery_value = '---%'
                break
        }
    }

    return (
        <BatteryStatusTile>
            <Typography>{battery_value}</Typography>
            <Icon name={battery_icon} color={icon_color} size={24} />
        </BatteryStatusTile>
    )
}

export default BatteryStatusView
